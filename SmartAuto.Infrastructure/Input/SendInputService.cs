using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Infrastructure.Input;

/// <summary>
/// Simulates keyboard and mouse input using the Win32 SendInput API via pure P/Invoke.
/// NEVER uses SendKeys, InputSimulator, or any third-party input library.
///
/// All coordinate conversions account for DPI scaling via
/// <see cref="GetSystemMetrics"/> SM_CXSCREEN / SM_CYSCREEN normalization.
/// Thread-safe: SendInput is called from any background thread.
/// </summary>
public sealed class SendInputService
{
    // ─── Win32 constants ─────────────────────────────────────────────────────
    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE         = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN     = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP       = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN    = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP      = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN   = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP     = 0x0040;
    private const uint MOUSEEVENTF_ABSOLUTE     = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK  = 0x4000;

    private const uint KEYEVENTF_KEYUP       = 0x0002;
    private const uint KEYEVENTF_SCANCODE    = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;

    // ─── P/Invoke structures ─────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx;
        public int    dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public nint   dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public nint   dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint  uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT   mi;
        [FieldOffset(0)] public KEYBDINPUT   ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint      type;
        public INPUTUNION u;
    }

    // ─── P/Invoke signatures ──────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    // ─── Fields ───────────────────────────────────────────────────────────────
    private readonly ILogger<SendInputService> _logger;

    public SendInputService(ILogger<SendInputService> logger)
    {
        _logger = logger;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the cursor to the given physical screen coordinates and performs a mouse click.
    /// Uses MOUSEEVENTF_ABSOLUTE + MOUSEEVENTF_VIRTUALDESK for multi-monitor support.
    /// </summary>
    public async Task ClickAsync(
        Point physicalPoint,
        MouseButton button,
        CancellationToken cancellationToken,
        bool doubleClick = false)
    {
        await Task.Run(() =>
        {
            // Normalize coordinates to the virtual desktop (0x0 → 65535x65535).
            var (dx, dy) = NormalizeToVirtualDesk(physicalPoint);

            var moveInput = BuildMouseInput(dx, dy, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK);

            var (downFlag, upFlag) = button switch
            {
                MouseButton.Left   => (MOUSEEVENTF_LEFTDOWN,   MOUSEEVENTF_LEFTUP),
                MouseButton.Right  => (MOUSEEVENTF_RIGHTDOWN,  MOUSEEVENTF_RIGHTUP),
                MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN,  MOUSEEVENTF_MIDDLEUP),
                _                  => (MOUSEEVENTF_LEFTDOWN,   MOUSEEVENTF_LEFTUP),
            };

            var downInput = BuildMouseInput(dx, dy, downFlag | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK);
            var upInput   = BuildMouseInput(dx, dy, upFlag   | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK);

            var inputs = doubleClick
                ? new[] { moveInput, downInput, upInput, downInput, upInput }
                : new[] { moveInput, downInput, upInput };

            SendOrThrow(inputs);

            _logger.LogDebug("MouseClick at ({X},{Y}) button={Button} double={Dbl}.",
                physicalPoint.X, physicalPoint.Y, button, doubleClick);

        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Types a string character by character using SendInput (Unicode mode).
    /// Optionally adds a per-character delay for applications that need it.
    /// </summary>
    public async Task TypeTextAsync(
        string text,
        int charDelayMs,
        CancellationToken cancellationToken)
    {
        foreach (var ch in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendUnicodeChar(ch);
            if (charDelayMs > 0)
                await Task.Delay(charDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a virtual key event (down or up) with optional modifiers.
    /// </summary>
    public Task SendKeyAsync(
        ushort virtualKey,
        ushort scanCode,
        ModifierKeys modifiers,
        bool isKeyDown,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var inputs = new List<INPUT>();

            // Press modifier keys first.
            if (modifiers.HasFlag(ModifierKeys.Shift))   inputs.Add(BuildKeyInput(0x10, 0, true));
            if (modifiers.HasFlag(ModifierKeys.Control)) inputs.Add(BuildKeyInput(0x11, 0, true));
            if (modifiers.HasFlag(ModifierKeys.Alt))     inputs.Add(BuildKeyInput(0x12, 0, true));
            if (modifiers.HasFlag(ModifierKeys.Win))     inputs.Add(BuildKeyInput(0x5B, 0, true));

            // Main key.
            inputs.Add(BuildKeyInput(virtualKey, scanCode, isKeyDown));

            // Release modifiers in reverse order.
            if (modifiers.HasFlag(ModifierKeys.Win))     inputs.Add(BuildKeyInput(0x5B, 0, false));
            if (modifiers.HasFlag(ModifierKeys.Alt))     inputs.Add(BuildKeyInput(0x12, 0, false));
            if (modifiers.HasFlag(ModifierKeys.Control)) inputs.Add(BuildKeyInput(0x11, 0, false));
            if (modifiers.HasFlag(ModifierKeys.Shift))   inputs.Add(BuildKeyInput(0x10, 0, false));

            SendOrThrow([.. inputs]);

        }, cancellationToken);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private void SendUnicodeChar(char ch)
    {
        var inputs = new[]
        {
            BuildUnicodeKeyInput(ch, down: true),
            BuildUnicodeKeyInput(ch, down: false),
        };
        SendOrThrow(inputs);
    }

    private static INPUT BuildMouseInput(int dx, int dy, uint flags)
        => new()
        {
            type = INPUT_MOUSE,
            u    = new INPUTUNION
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags },
            },
        };

    private static INPUT BuildKeyInput(ushort vk, ushort scan, bool down)
        => new()
        {
            type = INPUT_KEYBOARD,
            u    = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk    = vk,
                    wScan  = scan,
                    dwFlags = down ? 0u : KEYEVENTF_KEYUP,
                },
            },
        };

    private static INPUT BuildUnicodeKeyInput(char ch, bool down)
        => new()
        {
            type = INPUT_KEYBOARD,
            u    = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk    = 0,
                    wScan  = ch,
                    dwFlags = (down ? 0u : KEYEVENTF_KEYUP) | 0x0004u, // KEYEVENTF_UNICODE
                },
            },
        };

    private void SendOrThrow(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var err = Marshal.GetLastWin32Error();
            _logger.LogError("SendInput: sent {Sent}/{Total} inputs. Win32 error {Err}.",
                sent, inputs.Length, err);
        }
    }

    /// <summary>
    /// Converts physical screen coordinates to the normalized virtual desktop range [0, 65535].
    /// Required for MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK to work correctly on
    /// multi-monitor setups.
    /// </summary>
    private static (int dx, int dy) NormalizeToVirtualDesk(Point physical)
    {
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);

        if (vw == 0 || vh == 0) return (physical.X, physical.Y);

        int dx = (int)((physical.X - vx) * 65535.0 / vw);
        int dy = (int)((physical.Y - vy) * 65535.0 / vh);
        return (dx, dy);
    }
}
