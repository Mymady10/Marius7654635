namespace SmartAuto.Abstractions.Models;

/// <summary>Physical screen point (pixels, not DIP).</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>Physical screen rectangle.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public ScreenPoint Center => new(X + Width / 2, Y + Height / 2);
    public bool Contains(ScreenPoint p) => p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;
}

public enum MouseButton { Left, Right, Middle }

public readonly record struct KeyCombo(string Keys, bool Ctrl = false, bool Alt = false, bool Shift = false, bool Win = false);

/// <summary>Result from a selector strategy attempt.</summary>
public sealed class SelectorResult
{
    public required ScreenPoint ClickPoint { get; init; }
    public required string StrategyName { get; init; }
    public required int Confidence { get; init; } // 0-100
    public bool IsAbsoluteCoordinates { get; init; }
    public ScreenRect? BoundingRect { get; init; }
    public string? ElementText { get; init; }
}

/// <summary>Selector descriptor that travels with each recorded action.</summary>
public sealed class ElementSelector
{
    public string? AutomationId { get; init; }
    public string? Name { get; init; }
    public string? ClassName { get; init; }
    public string? ControlType { get; init; }
    public string? ProcessName { get; init; }
    public string? WindowTitle { get; init; }
    public IntPtr? WindowHandle { get; init; }
    public ScreenPoint? AbsolutePoint { get; init; }
    public ScreenRect? AbsoluteRect { get; init; }
    public string? ImageBase64 { get; init; }  // 32x32 anchor image
    public string? TextToFind { get; init; }
    public ColorRange? ColorRange { get; init; }
    public ScreenRect? SearchRegion { get; init; }  // limit search to region
}

public sealed class ColorRange
{
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public byte Tolerance { get; init; } = 15;
}

/// <summary>A raw bitmap frame captured from screen.</summary>
public sealed class CapturedFrame : IDisposable
{
    private bool _disposed;
    public required byte[] PixelData { get; init; }  // BGRA32
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public required double DpiX { get; init; }
    public required double DpiY { get; init; }
    public required ScreenRect Bounds { get; init; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>Progress update during script playback.</summary>
public sealed class PlaybackProgress
{
    public required int TotalActions { get; init; }
    public required int CompletedActions { get; init; }
    public required string CurrentActionName { get; init; }
    public required PlaybackStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum PlaybackStatus { Running, Paused, Completed, Failed, Cancelled }

public sealed class PlaybackResult
{
    public required bool Success { get; init; }
    public required int ActionsExecuted { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorActionId { get; init; }
    public Dictionary<string, object> Variables { get; init; } = [];
}

/// <summary>A raw action captured during recording (before conversion to typed ActionBase).</summary>
public sealed class RecordedAction
{
    public required string Type { get; init; }  // "MouseClick", "KeyPress", etc.
    public required DateTimeOffset Timestamp { get; init; }
    public ScreenPoint? Position { get; init; }
    public MouseButton? Button { get; init; }
    public bool IsDoubleClick { get; init; }
    public string? KeyName { get; init; }
    public int? VirtualKey { get; init; }
    public int? ScanCode { get; init; }
    public bool KeyDown { get; init; }
    public string? WindowTitle { get; init; }
    public string? ProcessName { get; init; }
    public IntPtr? WindowHandle { get; init; }
    public ScreenRect? WindowRect { get; init; }
    public string? ClassName { get; init; }
}

public sealed class ScriptMetadata
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Modified { get; init; }
    public int ActionCount { get; init; }
}

/// <summary>Lightweight script definition used across layers (full model in Domain).</summary>
public sealed class ScriptDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Version { get; init; } = "1.1";
    public string? Description { get; init; }
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Variables { get; init; } = [];
    public List<object> Actions { get; init; } = [];
}
