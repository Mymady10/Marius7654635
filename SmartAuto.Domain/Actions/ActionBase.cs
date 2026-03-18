using System.Text.Json.Serialization;
using SmartAuto.Abstractions;

namespace SmartAuto.Domain.Actions;

/// <summary>
/// Abstract base record for all SmartAuto automation actions.
/// Uses C# 12 primary constructors and System.Text.Json polymorphic serialization.
/// All concrete action types must be registered here with [JsonDerivedType].
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FindTextAndClickAction),    "FindTextAndClick")]
[JsonDerivedType(typeof(FindColorAndClickAction),   "FindColorAndClick")]
[JsonDerivedType(typeof(FindImageAndClickAction),   "FindImageAndClick")]
[JsonDerivedType(typeof(TypeTextAction),            "TypeText")]
[JsonDerivedType(typeof(SendInputAction),           "SendInput")]
[JsonDerivedType(typeof(WaitForConditionAction),    "WaitForCondition")]
[JsonDerivedType(typeof(IfConditionAction),         "IfCondition")]
[JsonDerivedType(typeof(LoopAction),                "Loop")]
[JsonDerivedType(typeof(DelayAction),               "Delay")]
[JsonDerivedType(typeof(MouseClickAction),          "MouseClick")]
public abstract record ActionBase : IAction
{
    /// <inheritdoc />
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc />
    public bool IsEnabled { get; init; } = true;

    /// <inheritdoc />
    public TimeSpan? Timeout { get; init; }

    /// <inheritdoc />
    public int? RetryCount { get; init; }

    /// <inheritdoc />
    public OnErrorBehavior OnError { get; init; } = OnErrorBehavior.Stop;
}

// ─── Concrete action records ────────────────────────────────────────────────

/// <summary>
/// Finds text using OCR (Windows.Media.Ocr → Tesseract fallback) and clicks the center of the match.
/// </summary>
public sealed record FindTextAndClickAction : ActionBase
{
    /// <summary>Text to search for.  Supports {{Variable}} interpolation.</summary>
    public required string SearchText { get; init; }

    /// <summary>Whether text matching is case-sensitive.</summary>
    public bool CaseSensitive { get; init; } = false;

    /// <summary>Minimum OCR confidence (0-100) required to accept the match.</summary>
    public int MinConfidence { get; init; } = 70;

    /// <summary>Optional search region to restrict OCR to a sub-area (logical pixels).</summary>
    public System.Drawing.Rectangle? SearchRegion { get; init; }
}

/// <summary>
/// Finds a pixel of a specific color (HSV tolerance) and clicks it.
/// </summary>
public sealed record FindColorAndClickAction : ActionBase
{
    /// <summary>Target RGB color to locate.</summary>
    public required System.Drawing.Color TargetColor { get; init; }

    /// <summary>Per-channel HSV tolerance.  Default: H=±15, S=±15, V=±15.</summary>
    public Abstractions.HsvTolerance Tolerance { get; init; } = new(15, 15, 15);

    /// <summary>Optional search region to restrict pixel search (logical pixels).</summary>
    public System.Drawing.Rectangle? SearchRegion { get; init; }
}

/// <summary>
/// Performs image template matching (TM_CCOEFF_NORMED ≥ threshold) and clicks the center of the match.
/// </summary>
public sealed record FindImageAndClickAction : ActionBase
{
    /// <summary>Base-64 encoded PNG reference image (32×32 px recommended anchor).</summary>
    public required string ReferenceImageBase64 { get; init; }

    /// <summary>Minimum template-match confidence (0-1, default 0.95).</summary>
    public double MatchThreshold { get; init; } = 0.95;

    /// <summary>Optional search region (logical pixels).</summary>
    public System.Drawing.Rectangle? SearchRegion { get; init; }
}

/// <summary>
/// Types text into the currently focused element using SendInput (pure P/Invoke).
/// Sensitive values are stored encrypted and rendered as &lt;masked&gt; in logs.
/// </summary>
public sealed record TypeTextAction : ActionBase
{
    /// <summary>Text to type.  Supports {{Variable}} interpolation.</summary>
    public required string Text { get; init; }

    /// <summary>Per-character delay in milliseconds (0 = as fast as possible).</summary>
    public int CharDelayMs { get; init; } = 0;

    /// <summary>Whether this field is sensitive (masked in logs / scripts).</summary>
    public bool IsSensitive { get; init; } = false;
}

/// <summary>
/// Sends a low-level keyboard input event via SendInput with full modifier support.
/// </summary>
public sealed record SendInputAction : ActionBase
{
    /// <summary>Virtual key code (VK_* constant).</summary>
    public required ushort VirtualKey { get; init; }

    /// <summary>Scan code (hardware-independent keyboard code).</summary>
    public ushort ScanCode { get; init; }

    /// <summary>Modifier keys to hold while sending (Ctrl, Alt, Shift, Win).</summary>
    public ModifierKeys Modifiers { get; init; } = ModifierKeys.None;

    /// <summary>Whether to send a key-down event (true) or key-up event (false).</summary>
    public bool IsKeyDown { get; init; } = true;
}

/// <summary>Modifier key flags for SendInput actions.</summary>
[Flags]
public enum ModifierKeys
{
    None    = 0,
    Shift   = 1 << 0,
    Control = 1 << 1,
    Alt     = 1 << 2,
    Win     = 1 << 3,
}

/// <summary>
/// Waits until a UI condition is satisfied or a timeout is reached.
/// </summary>
public sealed record WaitForConditionAction : ActionBase
{
    /// <summary>Condition expression to evaluate (DynamicExpresso syntax).</summary>
    public required string ConditionExpression { get; init; }

    /// <summary>Polling interval in milliseconds.</summary>
    public int PollIntervalMs { get; init; } = 500;
}

/// <summary>
/// Conditionally executes ThenActions or ElseActions based on a boolean expression.
/// </summary>
public sealed record IfConditionAction : ActionBase
{
    /// <summary>Boolean condition expression (DynamicExpresso syntax).</summary>
    public required string ConditionExpression { get; init; }

    /// <summary>Actions to execute when condition is true.</summary>
    public IReadOnlyList<ActionBase> ThenActions { get; init; } = [];

    /// <summary>Actions to execute when condition is false.</summary>
    public IReadOnlyList<ActionBase> ElseActions { get; init; } = [];
}

/// <summary>
/// Repeats a list of actions for a fixed count or while a condition is true.
/// An iteration guard prevents infinite loops (default max 100).
/// </summary>
public sealed record LoopAction : ActionBase
{
    /// <summary>Fixed iteration count (null = use WhileCondition).</summary>
    public int? IterationCount { get; init; }

    /// <summary>While condition expression; evaluated before each iteration.</summary>
    public string? WhileCondition { get; init; }

    /// <summary>Maximum iterations guard (default 100; prevents infinite loops).</summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>Actions to execute each loop iteration.</summary>
    public IReadOnlyList<ActionBase> BodyActions { get; init; } = [];
}

/// <summary>
/// Pauses script execution for the specified duration.
/// </summary>
public sealed record DelayAction : ActionBase
{
    /// <summary>Delay duration in milliseconds.</summary>
    public required int DelayMs { get; init; }
}

/// <summary>
/// Performs a physical mouse click at recorded or computed coordinates using SendInput.
/// </summary>
public sealed record MouseClickAction : ActionBase
{
    /// <summary>Screen coordinates in logical pixels at time of recording.</summary>
    public required System.Drawing.Point LogicalCoords { get; init; }

    /// <summary>Mouse button to click.</summary>
    public MouseButton Button { get; init; } = MouseButton.Left;

    /// <summary>Whether to perform a double-click.</summary>
    public bool IsDoubleClick { get; init; } = false;

    /// <summary>Window context captured at recording time for relative positioning.</summary>
    public WindowContext? WindowContext { get; init; }
}

/// <summary>Which mouse button to simulate.</summary>
public enum MouseButton { Left, Right, Middle }

/// <summary>Window context snapshot captured during recording.</summary>
public sealed record WindowContext(
    nint Handle,
    string Title,
    string ProcessName,
    string ClassName,
    System.Drawing.Rectangle Bounds);
