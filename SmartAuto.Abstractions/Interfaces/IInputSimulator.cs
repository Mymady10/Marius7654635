using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

public interface IInputSimulator
{
    /// <summary>Move mouse to physical screen coordinates and click.</summary>
    ValueTask ClickAsync(ScreenPoint point, MouseButton button = MouseButton.Left, CancellationToken ct = default);
    /// <summary>Double-click at the given point.</summary>
    ValueTask DoubleClickAsync(ScreenPoint point, CancellationToken ct = default);
    /// <summary>Type text using SendInput (no SendKeys).</summary>
    ValueTask TypeTextAsync(string text, int delayMs = 0, CancellationToken ct = default);
    /// <summary>Send a key combination (e.g. Ctrl+C).</summary>
    ValueTask SendKeysAsync(KeyCombo combo, CancellationToken ct = default);
}
