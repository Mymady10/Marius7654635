using System.Drawing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Extensions;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Strategy 2: Finds a pixel matching a specific color in HSV color space with per-channel tolerance.
/// Uses OpenCvSharp for HSV conversion + inRange mask.
/// DPI-aware: returns physical pixel coordinates.
/// </summary>
public sealed class ColorMatchSelectorStrategy : SelectorStrategyBase
{
    public override int    Priority => 2;
    public override string Name     => "ColorMatch";

    private readonly IScreenCaptureService _capture;

    public ColorMatchSelectorStrategy(
        ILogger<ColorMatchSelectorStrategy> logger,
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
        if (criteria.TargetColor is null)
            return null;

        var capture = await _capture.CaptureScreenAsync(
            criteria.TargetWindowHandle,
            criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor),
            cancellationToken).ConfigureAwait(false);

        return await Task.Run(() => FindColor(capture, criteria, context), cancellationToken)
                         .ConfigureAwait(false);
    }

    private SelectorResult? FindColor(
        CaptureResult capture, SelectorCriteria criteria, IExecutionContext context)
    {
        var color     = criteria.TargetColor!.Value;
        var tolerance = criteria.ColorTolerance ?? new HsvTolerance(15, 15, 15);

        // Convert RGB → OpenCV Mat (BGRA → BGR → HSV).
        using var srcMat = new Mat(capture.Height, capture.Width, MatType.CV_8UC4, capture.BgraPixels);
        using var bgrMat  = new Mat();
        using var hsvMat  = new Mat();
        Cv2.CvtColor(srcMat, bgrMat, ColorConversionCodes.BGRA2BGR);
        Cv2.CvtColor(bgrMat, hsvMat, ColorConversionCodes.BGR2HSV);

        // Convert target color to HSV.
        var (targetH, targetS, targetV) = RgbToHsv(color.R, color.G, color.B);

        // Build HSV range with tolerance.
        var lowerBound = new Scalar(
            Math.Max(0, targetH - tolerance.H),
            Math.Max(0, targetS - tolerance.S),
            Math.Max(0, targetV - tolerance.V));
        var upperBound = new Scalar(
            Math.Min(180, targetH + tolerance.H),
            Math.Min(255, targetS + tolerance.S),
            Math.Min(255, targetV + tolerance.V));

        using var mask    = new Mat();
        Cv2.InRange(hsvMat, lowerBound, upperBound, mask);

        // Find the largest connected component or first non-zero point.
        Cv2.FindNonZero(mask, out var points);
        if (points is null || points.Total() == 0)
            return null;

        // Compute centroid of matched region.
        var moments = Cv2.Moments(mask, binaryImage: true);
        if (moments.M00 == 0) return null;

        int cx = (int)(moments.M10 / moments.M00);
        int cy = (int)(moments.M01 / moments.M00);

        // Adjust for search region offset (coordinates are relative to capture, not screen).
        var regionOffset = criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor)?.Location
                          ?? Point.Empty;

        var physicalCenter = new Point(cx + regionOffset.X, cy + regionOffset.Y);
        var physicalBounds = new Rectangle(physicalCenter.X - 2, physicalCenter.Y - 2, 5, 5);

        // Confidence = matched pixels / total search area (clamped 0-100).
        var matchedPixels = (int)points.Total();
        var totalPixels   = capture.Width * capture.Height;
        var rawConfidence = Math.Clamp((double)matchedPixels / totalPixels * 10_000.0, 0, 100);
        var confidence    = (int)Math.Round(rawConfidence);

        return new SelectorResult(physicalCenter, physicalBounds, Name, confidence);
    }

    /// <summary>Converts RGB (0-255) to OpenCV HSV (H:0-180, S:0-255, V:0-255).</summary>
    private static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf));
        double min = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == rf)      h = 60.0 * (((gf - bf) / delta) % 6);
            else if (max == gf) h = 60.0 * (((bf - rf) / delta) + 2);
            else                h = 60.0 * (((rf - gf) / delta) + 4);
        }
        if (h < 0) h += 360;

        // OpenCV HSV: H / 2 to fit in [0, 180], S/V scaled to [0, 255]
        return (h / 2.0, (max == 0 ? 0 : delta / max) * 255.0, max * 255.0);
    }
}
