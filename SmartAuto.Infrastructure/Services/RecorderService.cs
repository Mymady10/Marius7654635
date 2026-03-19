using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Domain.Actions;
using SmartAuto.Infrastructure.Hooks;

namespace SmartAuto.Infrastructure.Services;

/// <summary>
/// Records user input actions via the global hooks and converts raw events
/// into a structured list of <see cref="ActionBase"/> instances.
///
/// Privacy: raw keystroke VK codes are never stored or logged as plaintext.
/// Sensitive fields are masked with <see cref="AppConstants.SensitivePlaceholder"/>.
/// </summary>
public sealed class RecorderService : IRecorderService, IAsyncDisposable
{
    private readonly GlobalHookManager         _hookManager;
    private readonly ILogger<RecorderService>  _logger;

    private CancellationTokenSource? _recordingCts;
    private Task?                    _recordingTask;
    private readonly List<ActionBase> _capturedActions = new();
    private readonly object           _lock = new();
    private DateTimeOffset            _lastEventTime;

    public RecorderService(GlobalHookManager hookManager, ILogger<RecorderService> logger)
    {
        _hookManager = hookManager;
        _logger      = logger;

        // Forward raw events to our EventCaptured event.
        _hookManager.EventCaptured += OnHookEventCaptured;
    }

    // ─── IRecorderService ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsRecording { get; private set; }

    /// <inheritdoc />
    public event EventHandler<RecordedEventArgs>? EventCaptured;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
        {
            _logger.LogWarning("RecorderService.StartAsync called while already recording.");
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _capturedActions.Clear();
            _lastEventTime = DateTimeOffset.UtcNow;
        }

        _recordingCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _recordingTask = ProcessEventsAsync(_recordingCts.Token);
        _hookManager.Start();
        IsRecording = true;

        _logger.LogInformation("Recording started.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAction>> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording)
            return Array.Empty<IAction>();

        _hookManager.Stop();
        IsRecording = false;

        _recordingCts?.Cancel();
        if (_recordingTask is not null)
        {
            try { await _recordingTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("Recording stopped. Captured {Count} actions.", _capturedActions.Count);

        lock (_lock)
        {
            return _capturedActions.AsReadOnly();
        }
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var rawEvent in _hookManager.ReadAllEventsAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var interActionDelay = (int)(rawEvent.Timestamp - _lastEventTime).TotalMilliseconds;
            if (interActionDelay > AppConstants.MinInterActionDelayMs && _capturedActions.Count > 0)
            {
                lock (_lock)
                {
                    _capturedActions.Add(new DelayAction
                    {
                        DelayMs     = Math.Min(interActionDelay, 5000), // cap at 5 s
                        Description = $"Wait {interActionDelay} ms",
                    });
                }
            }

            ActionBase? action = rawEvent.Kind switch
            {
                InputEventKind.MouseLeftDown or
                InputEventKind.MouseRightDown or
                InputEventKind.MouseMiddleDown or
                InputEventKind.MouseDoubleClick =>
                    new MouseClickAction
                    {
                        LogicalCoords = rawEvent.PhysicalPoint,  // will be made relative later
                        Button        = MapButton(rawEvent.Kind),
                        IsDoubleClick = rawEvent.Kind == InputEventKind.MouseDoubleClick,
                        Description   = $"Click at ({rawEvent.ScreenX},{rawEvent.ScreenY})",
                        WindowContext = rawEvent.WindowContext,
                    },

                InputEventKind.KeyDown =>
                    new SendInputAction
                    {
                        VirtualKey  = (ushort)rawEvent.VirtualKey,
                        ScanCode    = (ushort)rawEvent.ScanCode,
                        IsKeyDown   = true,
                        Description = AppConstants.SensitivePlaceholder, // never log VK
                    },

                InputEventKind.KeyUp =>
                    new SendInputAction
                    {
                        VirtualKey  = (ushort)rawEvent.VirtualKey,
                        ScanCode    = (ushort)rawEvent.ScanCode,
                        IsKeyDown   = false,
                        Description = AppConstants.SensitivePlaceholder,
                    },

                _ => null,
            };

            if (action is not null)
            {
                lock (_lock)
                {
                    _capturedActions.Add(action);
                }
            }

            _lastEventTime = rawEvent.Timestamp;
        }
    }

    private static MouseButton MapButton(InputEventKind kind) => kind switch
    {
        InputEventKind.MouseRightDown  => MouseButton.Right,
        InputEventKind.MouseMiddleDown => MouseButton.Middle,
        _                              => MouseButton.Left,
    };

    private void OnHookEventCaptured(object? sender, RecordedEventArgs e)
        => EventCaptured?.Invoke(this, e);

    public async ValueTask DisposeAsync()
    {
        _hookManager.EventCaptured -= OnHookEventCaptured;
        if (IsRecording)
            await StopAsync().ConfigureAwait(false);

        _recordingCts?.Dispose();
    }
}
