namespace SmartAuto.Abstractions;

/// <summary>
/// Provides DPI-aware screenshot capture of a monitor or window region
/// using Windows.Graphics.Capture (primary) with GDI fallback.
/// </summary>
public interface IScreenCaptureService : IDisposable
{
    /// <summary>
    /// Captures the full primary monitor or the monitor containing the given window.
    /// Returns raw BGRA pixel data with width and height.
    /// </summary>
    Task<CaptureResult> CaptureScreenAsync(
        nint windowHandle = 0,
        System.Drawing.Rectangle? region = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a screen capture operation.</summary>
public sealed record CaptureResult(
    byte[] BgraPixels,
    int Width,
    int Height,
    double DpiScale,
    DateTimeOffset CapturedAt);
