using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SmartAuto.Abstractions.Interfaces;
using SmartAuto.Abstractions.Models;

namespace SmartAuto.Infrastructure.Input;

/// <summary>
/// Pure P/Invoke SendInput implementation.
/// Never uses SendKeys, InputSimulator, or any legacy API.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32InputSimulator : IInputSimulator
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    public async ValueTask ClickAsync(ScreenPoint point, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        MoveTo(point);
        await Task.Delay(10, ct).ConfigureAwait(false);
        MouseClick(point, button);
    }

    public async ValueTask DoubleClickAsync(ScreenPoint point, CancellationToken ct = default)
    {
        MoveTo(point);
        await Task.Delay(10, ct).ConfigureAwait(false);
        MouseClick(point, MouseButton.Left);
        await Task.Delay(50, ct).ConfigureAwait(false);
        MouseClick(point, MouseButton.Left);
    }

    public async ValueTask TypeTextAsync(string text, int delayMs = 0, CancellationToken ct = default)
    {
        foreach (var ch in text)
        {
            ct.ThrowIfCancellationRequested();
            SendChar(ch);
            if (delayMs > 0)
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }
    }

    public ValueTask SendKeysAsync(KeyCombo combo, CancellationToken ct = default)
    {
        var inputs = BuildComboInputs(combo);
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return ValueTask.CompletedTask;
    }

    private void MoveTo(ScreenPoint p)
    {
        int w = GetSystemMetrics(SM_CXSCREEN), h = GetSystemMetrics(SM_CYSCREEN);
        int ax = (int)((double)p.X * 65535 / w), ay = (int)((double)p.Y * 65535 / h);
        var inp = new INPUT { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } } };
        SendInput(1, [inp], Marshal.SizeOf<INPUT>());
    }

    private void MouseClick(ScreenPoint p, MouseButton btn)
    {
        int w = GetSystemMetrics(SM_CXSCREEN), h = GetSystemMetrics(SM_CYSCREEN);
        int ax = (int)((double)p.X * 65535 / w), ay = (int)((double)p.Y * 65535 / h);
        var (dn, up) = btn switch
        {
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };
        INPUT[] inputs =
        [
            new() { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = dn | MOUSEEVENTF_ABSOLUTE } } },
            new() { type = INPUT_MOUSE, u = new INPUTUNION { mi = new MOUSEINPUT { dx = ax, dy = ay, dwFlags = up | MOUSEEVENTF_ABSOLUTE } } }
        ];
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private void SendChar(char c)
    {
        INPUT[] inputs =
        [
            new() { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE } } },
            new() { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } }
        ];
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT[] BuildComboInputs(KeyCombo combo)
    {
        var list = new List<INPUT>();
        void Key(ushort vk, bool up) => list.Add(new INPUT { type = INPUT_KEYBOARD, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } } });
        if (combo.Ctrl) Key(0x11, false);
        if (combo.Alt) Key(0x12, false);
        if (combo.Shift) Key(0x10, false);
        if (combo.Win) Key(0x5B, false);
        foreach (var ch in combo.Keys.ToUpperInvariant()) Key((ushort)ch, false);
        foreach (var ch in combo.Keys.ToUpperInvariant()) Key((ushort)ch, true);
        if (combo.Win) Key(0x5B, true);
        if (combo.Shift) Key(0x10, true);
        if (combo.Alt) Key(0x12, true);
        if (combo.Ctrl) Key(0x11, true);
        return [.. list];
    }
}
