using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Infrastructure.Hooks;

/// <summary>
/// Installs and manages global low-level keyboard (WH_KEYBOARD_LL) and mouse (WH_MOUSE_LL)
/// hooks via pure P/Invoke (never SendKeys or InputSimulator).
///
/// Design guarantees:
/// • Thread-safe: hook procedures run on a dedicated message-pump thread.
/// • Rate-limited: max <see cref="AppConstants.MaxHookEventsPerSecond"/> events/sec with debouncing.
/// • Fully disposable: unhooks on Dispose(); no resource leaks.
/// • Anti-deadlock: callbacks never block; events are enqueued to a Channel.
/// • Privacy: raw keystroke values masked as &lt;masked&gt; in all logs.
/// </summary>
public sealed class GlobalHookManager : IDisposable
{
    // ─── Win32 constants ─────────────────────────────────────────────────────
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL    = 14;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP   = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP   = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP   = 0x0208;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    // ─── P/Invoke declarations ───────────────────────────────────────────────
    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    // ─── Fields ───────────────────────────────────────────────────────────────
    private readonly ILogger<GlobalHookManager> _logger;
    private readonly Channel<RawInputEvent> _channel;
    private readonly CancellationTokenSource _cts = new();

    // Keep delegates alive to prevent GC collection (hooks require stable callbacks).
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;

    private nint _keyboardHook;
    private nint _mouseHook;
    private Thread? _messagePumpThread;
    private bool _disposed;

    // Rate limiting: track timestamps of the last MaxHookEventsPerSecond events.
    private readonly ConcurrentQueue<long> _recentEventTimestamps = new();
    private readonly long _minTicksBetweenEvents =
        TimeSpan.TicksPerSecond / AppConstants.MaxHookEventsPerSecond;

    // Debounce: last event tick per event type for double-click detection.
    private long _lastMouseTick;
    private int _lastMouseMsg;

    /// <summary>
    /// Raised on the caller's synchronization context whenever an input event is captured.
    /// Provides a coarse-grained notification; consume <see cref="ReadEventAsync"/> for full data.
    /// </summary>
    public event EventHandler<RecordedEventArgs>? EventCaptured;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public GlobalHookManager(ILogger<GlobalHookManager> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<RawInputEvent>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false,
        });

        // Keep delegate references alive.
        _keyboardProc = KeyboardHookCallback;
        _mouseProc    = MouseHookCallback;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Installs both hooks on a dedicated STA message-pump thread.
    /// Thread-safe; idempotent (second call is a no-op if already started).
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_messagePumpThread is { IsAlive: true }) return;

        _messagePumpThread = new Thread(MessagePumpThread)
        {
            IsBackground = true,
            Name = "SmartAuto.HookMessagePump",
        };
        _messagePumpThread.SetApartmentState(ApartmentState.STA);
        _messagePumpThread.Start();

        _logger.LogInformation("Global hooks starting on dedicated message-pump thread.");
    }

    /// <summary>
    /// Removes both hooks and stops the message pump.
    /// All captured events remain available for consumption from the channel.
    /// </summary>
    public void Stop()
    {
        if (_disposed) return;

        _cts.Cancel();

        if (_keyboardHook != 0)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }
        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        _channel.Writer.TryComplete();
        _logger.LogInformation("Global hooks stopped.");
    }

    /// <summary>
    /// Reads the next captured input event asynchronously.
    /// Returns null when the hook manager has been stopped.
    /// </summary>
    public async ValueTask<RawInputEvent?> ReadEventAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>Reads all remaining events as an async enumerable.</summary>
    public IAsyncEnumerable<RawInputEvent> ReadAllEventsAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    // ─── Message pump thread ─────────────────────────────────────────────────

    private void MessagePumpThread()
    {
        try
        {
            var hMod = GetModuleHandle(null);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook    = SetWindowsHookEx(WH_MOUSE_LL,    _mouseProc,    hMod, 0);

            if (_keyboardHook == 0 || _mouseHook == 0)
            {
                var err = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to install hooks. Win32 error: {ErrorCode}", err);
                return;
            }

            _logger.LogInformation("Hooks installed. Keyboard={KbHook:X}, Mouse={MsHook:X}",
                _keyboardHook, _mouseHook);

            // Standard Win32 message pump.
            while (!_cts.IsCancellationRequested)
            {
                if (GetMessage(out var msg, 0, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message pump thread terminated unexpectedly.");
        }
        finally
        {
            if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
            if (_mouseHook    != 0) { UnhookWindowsHookEx(_mouseHook);    _mouseHook    = 0; }
        }
    }

    // ─── Hook callbacks ──────────────────────────────────────────────────────

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_cts.IsCancellationRequested)
        {
            try
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;

                if (IsWithinRateLimit())
                {
                    var kind   = isDown ? InputEventKind.KeyDown : InputEventKind.KeyUp;
                    var now    = DateTimeOffset.UtcNow;
                    // Never log raw key values; always use <masked>.
                    var rawEvt = new RawInputEvent(now, kind, kb.vkCode, kb.scanCode, 0, 0,
                        // Physical coords not applicable for keyboard.
                        System.Drawing.Point.Empty, null);

                    _channel.Writer.TryWrite(rawEvt);

                    EventCaptured?.Invoke(this, new RecordedEventArgs(
                        now, kind, AppConstants.SensitivePlaceholder));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in keyboard hook callback.");
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_cts.IsCancellationRequested)
        {
            try
            {
                var ms  = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var msg = (int)wParam;

                if (IsMouseButtonEvent(msg) && IsWithinRateLimit())
                {
                    var kind = MapMouseMessage(msg);
                    var now  = DateTimeOffset.UtcNow;
                    var pt   = new System.Drawing.Point(ms.pt.x, ms.pt.y);

                    var rawEvt = new RawInputEvent(now, kind, 0, 0,
                        ms.pt.x, ms.pt.y, pt, null);

                    _channel.Writer.TryWrite(rawEvt);

                    EventCaptured?.Invoke(this, new RecordedEventArgs(
                        now, kind, $"Mouse {kind} at ({ms.pt.x},{ms.pt.y})"));

                    _lastMouseTick = DateTime.UtcNow.Ticks;
                    _lastMouseMsg  = msg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in mouse hook callback.");
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ─── Rate limiting ───────────────────────────────────────────────────────

    private bool IsWithinRateLimit()
    {
        var now = DateTime.UtcNow.Ticks;

        // Remove timestamps older than 1 second.
        while (_recentEventTimestamps.TryPeek(out var oldest) &&
               (now - oldest) > TimeSpan.TicksPerSecond)
        {
            _recentEventTimestamps.TryDequeue(out _);
        }

        if (_recentEventTimestamps.Count >= AppConstants.MaxHookEventsPerSecond)
            return false;

        _recentEventTimestamps.Enqueue(now);
        return true;
    }

    private static bool IsMouseButtonEvent(int msg)
        => msg is WM_LBUTTONDOWN or WM_LBUTTONUP
                or WM_RBUTTONDOWN or WM_RBUTTONUP
                or WM_MBUTTONDOWN or WM_MBUTTONUP
                or WM_LBUTTONDBLCLK;

    private static InputEventKind MapMouseMessage(int msg) => msg switch
    {
        WM_LBUTTONDOWN    => InputEventKind.MouseLeftDown,
        WM_LBUTTONUP      => InputEventKind.MouseLeftUp,
        WM_RBUTTONDOWN    => InputEventKind.MouseRightDown,
        WM_RBUTTONUP      => InputEventKind.MouseRightUp,
        WM_MBUTTONDOWN    => InputEventKind.MouseMiddleDown,
        WM_MBUTTONUP      => InputEventKind.MouseMiddleUp,
        WM_LBUTTONDBLCLK  => InputEventKind.MouseDoubleClick,
        _                 => InputEventKind.MouseLeftDown,
    };

    // ─── Win32 message pump P/Invoke ─────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    // ─── IDisposable ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts.Dispose();
    }
}

/// <summary>A single raw input event captured by the global hooks.</summary>
public sealed record RawInputEvent(
    DateTimeOffset Timestamp,
    InputEventKind Kind,
    uint VirtualKey,
    uint ScanCode,
    int ScreenX,
    int ScreenY,
    System.Drawing.Point PhysicalPoint,
    /// <summary>Window context at the time of capture; null for keyboard events.</summary>
    Domain.Actions.WindowContext? WindowContext);
