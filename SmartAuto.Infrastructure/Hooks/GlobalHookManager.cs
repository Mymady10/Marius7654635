using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions.Interfaces;
using SmartAuto.Abstractions.Models;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Exceptions;

namespace SmartAuto.Infrastructure.Hooks;

/// <summary>
/// Installs WH_KEYBOARD_LL and WH_MOUSE_LL global hooks via pure P/Invoke.
/// Rate-limited to <see cref="AppConstants.MaxHookEventsPerSecond"/> events/sec.
/// Fully disposable and thread-safe.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalHookManager : IRecorderService
{
    // ── Win32 constants ──────────────────────────────────────────────────────
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;

    // ── P/Invoke ─────────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ── Fields ────────────────────────────────────────────────────────────────
    private readonly ILogger<GlobalHookManager> _logger;
    private readonly Subject<RecordedAction> _subject = new();
    private readonly object _lock = new();
    private readonly TimeSpan _minInterval =
        TimeSpan.FromMilliseconds(1000.0 / AppConstants.MaxHookEventsPerSecond);

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private LowLevelHookProc? _kbCallback;
    private LowLevelHookProc? _msCallback;
    private DateTimeOffset _lastEvent = DateTimeOffset.MinValue;
    private bool _isRecording;
    private bool _disposed;

    public bool IsRecording => _isRecording;
    public IObservable<RecordedAction> ActionStream => _subject;

    public GlobalHookManager(ILogger<GlobalHookManager> logger) => _logger = logger;

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_isRecording) return Task.CompletedTask;
            InstallHooks();
            _isRecording = true;
            _logger.LogInformation("Recording started – global hooks installed");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_isRecording) return Task.CompletedTask;
            RemoveHooks();
            _isRecording = false;
            _logger.LogInformation("Recording stopped – global hooks removed");
        }
        return Task.CompletedTask;
    }

    private void InstallHooks()
    {
        using var proc = Process.GetCurrentProcess();
        using var mod = proc.MainModule ?? throw new HookException("Cannot obtain main module");
        var hMod = GetModuleHandle(mod.ModuleName);

        _kbCallback = KeyboardProc;
        _msCallback = MouseProc;

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbCallback, hMod, 0);
        if (_keyboardHook == IntPtr.Zero)
            throw new HookException($"Keyboard hook failed: {Marshal.GetLastWin32Error()}");

        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _msCallback, hMod, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            throw new HookException($"Mouse hook failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private void RemoveHooks()
    {
        if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        _kbCallback = null;
        _msCallback = null;
    }

    private bool RateLimited()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastEvent < _minInterval) return true;
        _lastEvent = now;
        return false;
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !RateLimited())
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            var ctx = GetWindowContext();
            _subject.OnNext(new RecordedAction
            {
                Type = "KeyPress",
                Timestamp = DateTimeOffset.UtcNow,
                VirtualKey = (int)kb.vkCode,
                ScanCode = (int)kb.scanCode,
                KeyDown = isDown,
                KeyName = $"VK_{kb.vkCode}",
                WindowTitle = ctx.title,
                ProcessName = ctx.proc,
                WindowHandle = ctx.hwnd,
                WindowRect = ctx.rect
            });
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !RateLimited())
        {
            var msg = (int)wParam;
            if (msg is WM_LBUTTONDOWN or WM_LBUTTONDBLCLK or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
            {
                var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var ctx = GetWindowContext();
                var btn = msg == WM_RBUTTONDOWN ? MouseButton.Right
                    : msg == WM_MBUTTONDOWN ? MouseButton.Middle : MouseButton.Left;
                _subject.OnNext(new RecordedAction
                {
                    Type = "MouseClick",
                    Timestamp = DateTimeOffset.UtcNow,
                    Position = new ScreenPoint(ms.pt.X, ms.pt.Y),
                    Button = btn,
                    IsDoubleClick = msg == WM_LBUTTONDBLCLK,
                    WindowTitle = ctx.title,
                    ProcessName = ctx.proc,
                    WindowHandle = ctx.hwnd,
                    WindowRect = ctx.rect
                });
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private (string title, string proc, IntPtr hwnd, ScreenRect? rect) GetWindowContext()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, sb, 256);
            GetWindowThreadProcessId(hwnd, out var pid);
            var p = Process.GetProcessById((int)pid);
            ScreenRect? rect = null;
            if (GetWindowRect(hwnd, out var r))
                rect = new ScreenRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            return (sb.ToString(), p.ProcessName, hwnd, rect);
        }
        catch { return (string.Empty, string.Empty, IntPtr.Zero, null); }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        lock (_lock) { RemoveHooks(); }
        _subject.OnCompleted();
        _subject.Dispose();
        return ValueTask.CompletedTask;
    }
}
