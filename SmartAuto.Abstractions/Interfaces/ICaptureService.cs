using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

public interface ICaptureService : IAsyncDisposable
{
    /// <summary>Capture the full primary monitor (or virtual screen) using Windows.Graphics.Capture.</summary>
    Task<CapturedFrame> CaptureScreenAsync(CancellationToken ct = default);
    /// <summary>Capture a specific region in physical pixels.</summary>
    Task<CapturedFrame> CaptureRegionAsync(ScreenRect region, CancellationToken ct = default);
}
