namespace SmartAuto.Abstractions;

/// <summary>
/// Strategy for finding a UI element on screen using a specific technique.
/// Implementations are tried in priority order by the selector engine.
/// </summary>
public interface ISelectorStrategy
{
    /// <summary>
    /// Priority order (lower = tried first). Standard order:
    /// 0=UIA, 1=OCR, 2=ColorMatch, 3=ImageTemplate, 4=AbsoluteCoords.
    /// </summary>
    int Priority { get; }

    /// <summary>Human-readable name for logging and debug overlay.</summary>
    string Name { get; }

    /// <summary>
    /// Attempts to locate the element matching <paramref name="criteria"/> on screen.
    /// Returns null when the element cannot be found with sufficient confidence.
    /// </summary>
    /// <param name="criteria">Search parameters provided by the action.</param>
    /// <param name="context">Execution context containing variables and correlation ID.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// A <see cref="SelectorResult"/> with physical screen coordinates + confidence,
    /// or <c>null</c> if the strategy could not locate the element.
    /// </returns>
    ValueTask<SelectorResult?> TryFindAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input parameters for element search strategies.
/// </summary>
public sealed record SelectorCriteria(
    /// <summary>Target window HWND – used to scope UIA searches.</summary>
    nint TargetWindowHandle,
    /// <summary>Text to search for (OCR / UIA Name).</summary>
    string? SearchText = null,
    /// <summary>UI Automation AutomationId of the target element.</summary>
    string? AutomationId = null,
    /// <summary>UIA ClassName filter.</summary>
    string? ClassName = null,
    /// <summary>RGB color to find (used by color-match strategy).</summary>
    System.Drawing.Color? TargetColor = null,
    /// <summary>HSV tolerance for each channel (H, S, V) – default ±15.</summary>
    HsvTolerance? ColorTolerance = null,
    /// <summary>Reference image bytes for template-match strategy.</summary>
    byte[]? ReferenceImageBytes = null,
    /// <summary>Minimum template-match confidence threshold (0-1, default 0.95).</summary>
    double TemplateMatchThreshold = 0.95,
    /// <summary>Absolute fallback coordinates in logical pixels.</summary>
    System.Drawing.Point? AbsoluteCoords = null,
    /// <summary>Search region (null = full screen).</summary>
    System.Drawing.Rectangle? SearchRegion = null);

/// <summary>HSV per-channel tolerance for color matching.</summary>
public record struct HsvTolerance(double H = 15, double S = 15, double V = 15);

/// <summary>
/// Result returned by a successful selector strategy.
/// Coordinates are always in physical (raw) screen pixels.
/// </summary>
public sealed record SelectorResult(
    /// <summary>Center of the found element in physical pixels.</summary>
    System.Drawing.Point PhysicalCenter,
    /// <summary>Bounding rectangle in physical pixels.</summary>
    System.Drawing.Rectangle PhysicalBounds,
    /// <summary>Strategy that produced this result.</summary>
    string StrategyName,
    /// <summary>Confidence score 0-100.</summary>
    int Confidence,
    /// <summary>Optional anchor point for relative positioning (physical pixels).</summary>
    System.Drawing.Point? AnchorPhysical = null);
