namespace SmartAuto.Abstractions;

/// <summary>
/// Service responsible for capturing global keyboard and mouse events.
/// Implemented in Infrastructure via low-level P/Invoke hooks (WH_KEYBOARD_LL + WH_MOUSE_LL).
/// Never persists raw keystrokes; sensitive input masked as &lt;masked&gt; in all output.
/// </summary>
public interface IRecorderService
{
    /// <summary>Starts recording global input events.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops recording and returns the captured action list.</summary>
    Task<IReadOnlyList<IAction>> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>True when recording is active.</summary>
    bool IsRecording { get; }

    /// <summary>Raised on the calling thread's synchronization context when a new raw event is captured.</summary>
    event EventHandler<RecordedEventArgs>? EventCaptured;
}

/// <summary>Event args for a single captured input event.</summary>
public sealed record RecordedEventArgs(
    DateTimeOffset Timestamp,
    InputEventKind Kind,
    string Description);

/// <summary>Categorizes the type of input event captured.</summary>
public enum InputEventKind
{
    KeyDown,
    KeyUp,
    MouseLeftDown,
    MouseLeftUp,
    MouseRightDown,
    MouseRightUp,
    MouseMiddleDown,
    MouseMiddleUp,
    MouseDoubleClick,
}
