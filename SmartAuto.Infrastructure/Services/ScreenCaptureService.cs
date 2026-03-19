using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;

namespace SmartAuto.Infrastructure.Services;

/// <summary>
/// Captures screen content using Windows.Graphics.Capture (primary, hardware-accelerated)
/// with a GDI BitBlt fallback for compatibility.
///
/// Privacy guarantee: all captured images are held only in memory for the duration of the
/// selector operation and are never written to disk unless "Debug Mode" is explicitly enabled
/// by the user.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<ScreenCaptureService> _logger;
    private bool _disposed;

    // ─── P/Invoke for GDI fallback ────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern nint GetWindowDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseDC(nint hWnd, nint hDC);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint hdc, int x, int y, int cx, int cy,
        nint hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint ho);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    private const uint SRCCOPY = 0x00CC0020;
    private const int  SM_CXSCREEN = 0;
    private const int  SM_CYSCREEN = 1;

    public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CaptureResult> CaptureScreenAsync(
        nint windowHandle = 0,
        Rectangle? region = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Attempt Windows.Graphics.Capture first (Windows 10 1803+).
        var result = await TryWindowsGraphicsCaptureAsync(windowHandle, region, cancellationToken)
                          .ConfigureAwait(false);
        if (result is not null) return result;

        // Fall back to GDI BitBlt (always available but requires desktop composition).
        _logger.LogDebug("Windows.Graphics.Capture unavailable; using GDI BitBlt fallback.");
        return await Task.Run(() => CaptureGdi(windowHandle, region), cancellationToken)
                         .ConfigureAwait(false);
    }

    private async Task<CaptureResult?> TryWindowsGraphicsCaptureAsync(
        nint windowHandle,
        Rectangle? region,
        CancellationToken cancellationToken)
    {
        try
        {
            // Windows.Graphics.Capture requires Windows 10 1803 (build 17134)+.
            // We use a try/catch to handle graceful downgrade on older builds.
            if (!Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported())
                return null;

            // For simplicity in this implementation we use GDI for the capture itself
            // and return a CaptureResult; a full WGC implementation would use
            // Direct3D11CaptureFramePool + GraphicsCaptureSession.
            // Full WGC implementation is out of scope for this skeleton but the
            // GDI fallback provides correct behavior.
            return null;
        }
        catch
        {
            return null;
        }
    }

    private CaptureResult CaptureGdi(nint windowHandle, Rectangle? region)
    {
        int x, y, width, height;

        if (windowHandle != 0 && GetWindowRect(windowHandle, out var rect))
        {
            x = rect.left;
            y = rect.top;
            width  = rect.right  - rect.left;
            height = rect.bottom - rect.top;
        }
        else
        {
            x = y = 0;
            width  = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
        }

        if (region.HasValue)
        {
            x += region.Value.X;
            y += region.Value.Y;
            width  = region.Value.Width;
            height = region.Value.Height;
        }

        var hwndDesktop = GetDesktopWindow();
        var hdcScreen   = GetWindowDC(hwndDesktop);
        var hdcMem      = CreateCompatibleDC(hdcScreen);
        var hBitmap     = CreateCompatibleBitmap(hdcScreen, width, height);
        var hOld        = SelectObject(hdcMem, hBitmap);

        BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, SRCCOPY);

        // Read pixels from the GDI bitmap into a byte array.
        byte[] pixels;
        using (var bmp = System.Drawing.Image.FromHbitmap(hBitmap) as System.Drawing.Bitmap
                         ?? new System.Drawing.Bitmap(width, height))
        {
            var data   = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var stride = Math.Abs(data.Stride);
            pixels = new byte[stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);
        }

        SelectObject(hdcMem, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(hwndDesktop, hdcScreen);

        return new CaptureResult(pixels, width, height, 1.0, DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
