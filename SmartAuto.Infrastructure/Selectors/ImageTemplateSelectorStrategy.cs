using System.Drawing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SmartAuto.Abstractions;
using SmartAuto.Common.Extensions;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Strategy 3: Image template matching using OpenCvSharp TM_CCOEFF_NORMED.
/// Requires a reference image (32×32 px anchor recommended).
/// Minimum confidence threshold: <see cref="AppConstants.TemplateMatchThreshold"/> (0.95).
/// DPI-aware: returns physical pixel coordinates.
/// </summary>
public sealed class ImageTemplateSelectorStrategy : SelectorStrategyBase
{
    public override int    Priority => 3;
    public override string Name     => "ImageTemplate";

    private readonly IScreenCaptureService _capture;

    public ImageTemplateSelectorStrategy(
        ILogger<ImageTemplateSelectorStrategy> logger,
        SelectorLruCache cache,
        IScreenCaptureService capture)
        : base(logger, cache)
    {
        _capture = capture;
    }

    protected override async ValueTask<SelectorResult?> FindCoreAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (criteria.ReferenceImageBytes is null || criteria.ReferenceImageBytes.Length == 0)
            return null;

        var capture = await _capture.CaptureScreenAsync(
            criteria.TargetWindowHandle,
            criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor),
            cancellationToken).ConfigureAwait(false);

        return await Task.Run(() =>
            MatchTemplate(capture, criteria, context), cancellationToken).ConfigureAwait(false);
    }

    private SelectorResult? MatchTemplate(
        CaptureResult capture, SelectorCriteria criteria, IExecutionContext context)
    {
        // Load reference image from bytes.
        using var templateMat = Cv2.ImDecode(criteria.ReferenceImageBytes!, ImreadModes.Color);
        if (templateMat.Empty()) return null;

        // Prepare source image (BGRA → BGR grayscale for performance).
        using var srcMat = new Mat(
            capture.Height, capture.Width, MatType.CV_8UC4, capture.BgraPixels);
        using var srcGray  = new Mat();
        using var tmplGray = new Mat();
        Cv2.CvtColor(srcMat,      srcGray,  ColorConversionCodes.BGRA2GRAY);
        Cv2.CvtColor(templateMat, tmplGray, ColorConversionCodes.BGR2GRAY);

        // Run template matching.
        using var result = new Mat();
        Cv2.MatchTemplate(srcGray, tmplGray, result, TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

        var threshold = criteria.TemplateMatchThreshold;
        if (maxVal < threshold)
        {
            Logger.LogDebug("[ImageTemplate] Best match score {Score:F4} below threshold {Thresh:F4}.",
                maxVal, threshold);
            return null;
        }

        // maxLoc is the top-left corner of the best match region.
        var regionOffset = criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor)?.Location
                          ?? Point.Empty;

        var physX = maxLoc.X + templateMat.Width  / 2 + regionOffset.X;
        var physY = maxLoc.Y + templateMat.Height / 2 + regionOffset.Y;

        var physicalCenter = new Point(physX, physY);
        var physicalBounds = new Rectangle(
            maxLoc.X + regionOffset.X,
            maxLoc.Y + regionOffset.Y,
            templateMat.Width,
            templateMat.Height);

        var confidence = ComputeConfidence(maxVal, threshold, hasAnchor: true);

        return new SelectorResult(physicalCenter, physicalBounds, Name, confidence);
    }
}
