namespace SmartAuto.Abstractions;

/// <summary>
/// Service that provides visual debug overlay (transparent topmost window) 
/// to highlight elements before/during action execution.
/// </summary>
public interface IOverlayService
{
    /// <summary>
    /// Shows a highlight rectangle at the given physical screen coordinates for the specified duration.
    /// Must be non-blocking (fire-and-forget with internal timer).
    /// </summary>
    Task HighlightAsync(
        System.Drawing.Rectangle physicalBounds,
        TimeSpan duration,
        System.Drawing.Color? color = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes all active highlights immediately.</summary>
    void ClearAll();
}
