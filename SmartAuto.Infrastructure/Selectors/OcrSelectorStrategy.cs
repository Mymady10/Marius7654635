using System.Drawing;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Extensions;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Strategy 1: OCR-based text search using Windows.Media.Ocr (primary)
/// with TesseractOCR 5 as fallback.
/// Preprocessing: grayscale + threshold via OpenCvSharp for improved accuracy.
/// Returns the bounding rectangle of the first matching text word/line.
/// </summary>
public sealed class OcrSelectorStrategy : SelectorStrategyBase
{
    public override int    Priority => 1;
    public override string Name     => "OCR";

    private readonly IScreenCaptureService _capture;

    public OcrSelectorStrategy(
        ILogger<OcrSelectorStrategy> logger,
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
        if (string.IsNullOrWhiteSpace(criteria.SearchText))
            return null;

        var capture = await _capture.CaptureScreenAsync(
            criteria.TargetWindowHandle,
            criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor),
            cancellationToken).ConfigureAwait(false);

        // Try Windows.Media.Ocr first, fall back to Tesseract if unavailable.
        var result = await TryWindowsMediaOcrAsync(capture, criteria, context, cancellationToken)
                     .ConfigureAwait(false);

        result ??= await TryTesseractOcrAsync(capture, criteria, context, cancellationToken)
                        .ConfigureAwait(false);

        return result;
    }

    private async ValueTask<SelectorResult?> TryWindowsMediaOcrAsync(
        CaptureResult capture,
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Windows.Media.Ocr requires WinRT; only available on Windows 10+.
            // In a .NET 8 Windows project this is accessible via target framework moniker.
            // We use reflection-style access to remain compilable on dev machines without WinRT stubs.

            // Preprocess image for better OCR accuracy.
            var processedBytes = PreprocessForOcr(capture);

            // Build a SoftwareBitmap from the processed BGRA bytes.
            using var stream   = new System.IO.MemoryStream(processedBytes);
            var bitmapDecoder  = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(
                stream.AsRandomAccessStream()).AsTask(cancellationToken).ConfigureAwait(false);
            var softwareBitmap = await bitmapDecoder
                .GetSoftwareBitmapAsync()
                .AsTask(cancellationToken).ConfigureAwait(false);

            var ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine is null)
            {
                Logger.LogDebug("[OCR/WinRT] No OCR language pack available; falling back to Tesseract.");
                return null;
            }

            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap)
                                           .AsTask(cancellationToken).ConfigureAwait(false);

            return FindTextInOcrResult(ocrResult, criteria, capture, context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "[OCR/WinRT] Windows.Media.Ocr failed; will try Tesseract.");
            return null;
        }
    }

    private async ValueTask<SelectorResult?> TryTesseractOcrAsync(
        CaptureResult capture,
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var processedBytes = PreprocessForOcr(capture);

                // Use Tesseract engine via the Tesseract NuGet package.
                using var engine = new Tesseract.TesseractEngine(@"./tessdata", "eng",
                    Tesseract.EngineMode.Default);
                using var img    = Tesseract.Pix.LoadFromMemory(processedBytes);
                using var page   = engine.Process(img);

                var text = page.GetText();
                if (string.IsNullOrWhiteSpace(text)) return null;

                // Find search text in Tesseract's word-level bounding boxes.
                using var iter = page.GetIterator();
                iter.Begin();
                do
                {
                    if (iter.TryGetBoundingBox(Tesseract.PageIteratorLevel.Word, out var box))
                    {
                        var wordText = iter.GetText(Tesseract.PageIteratorLevel.Word)?.Trim() ?? string.Empty;
                        if (WordMatches(wordText, criteria.SearchText!))
                        {
                            var regionOffset = criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor)?.Location
                                              ?? Point.Empty;

                            var physicalBounds = new Rectangle(
                                box.X1 + regionOffset.X,
                                box.Y1 + regionOffset.Y,
                                box.Width,
                                box.Height);
                            var physicalCenter = physicalBounds.Center();

                            var confidence = Math.Clamp((int)page.GetMeanConfidence(), 0, 100);
                            return new SelectorResult(physicalCenter, physicalBounds, $"{Name}/Tesseract", confidence);
                        }
                    }
                }
                while (iter.Next(Tesseract.PageIteratorLevel.Word));

                return (SelectorResult?)null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex, "[OCR/Tesseract] Failed.");
                return null;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private SelectorResult? FindTextInOcrResult(
        Windows.Media.Ocr.OcrResult ocrResult,
        SelectorCriteria criteria,
        CaptureResult capture,
        IExecutionContext context)
    {
        var regionOffset = criteria.SearchRegion?.ToPhysical(context.DpiScaleFactor)?.Location
                          ?? Point.Empty;

        foreach (var line in ocrResult.Lines)
        {
            foreach (var word in line.Words)
            {
                if (!WordMatches(word.Text, criteria.SearchText!)) continue;

                var wr = word.BoundingRect;
                var physicalBounds = new Rectangle(
                    (int)wr.X + regionOffset.X,
                    (int)wr.Y + regionOffset.Y,
                    (int)wr.Width,
                    (int)wr.Height);
                var physicalCenter = physicalBounds.Center();
                var confidence     = Math.Clamp((int)(word.Confidence ?? 80), 0, 100);

                return new SelectorResult(physicalCenter, physicalBounds, $"{Name}/WinRT", confidence);
            }
        }
        return null;
    }

    private static bool WordMatches(string word, string searchText)
        => word.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts BGRA capture to grayscale + adaptive threshold PNG bytes
    /// for improved OCR accuracy on varied backgrounds.
    /// </summary>
    private static byte[] PreprocessForOcr(CaptureResult capture)
    {
        using var srcMat  = new OpenCvSharp.Mat(
            capture.Height, capture.Width, OpenCvSharp.MatType.CV_8UC4, capture.BgraPixels);
        using var grayMat = new OpenCvSharp.Mat();
        using var thrMat  = new OpenCvSharp.Mat();

        OpenCvSharp.Cv2.CvtColor(srcMat, grayMat, OpenCvSharp.ColorConversionCodes.BGRA2GRAY);
        OpenCvSharp.Cv2.AdaptiveThreshold(
            grayMat, thrMat, 255,
            OpenCvSharp.AdaptiveThresholdTypes.GaussianC,
            OpenCvSharp.ThresholdTypes.Binary,
            blockSize: 11, c: 2);

        OpenCvSharp.Cv2.ImEncode(".png", thrMat, out var buf);
        return buf;
    }
}
