using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Domain.Actions;
using SmartAuto.Domain.Models;

namespace SmartAuto.ViewModels;

/// <summary>
/// ViewModel for the Record page.
/// Observes recording state and exposes Start/Stop commands.
/// All work runs on background threads; UI updates are marshalled via DispatcherQueue.
/// </summary>
public sealed partial class RecordViewModel : ObservableObject
{
    private readonly IRecorderService               _recorder;
    private readonly ILogger<RecordViewModel>       _logger;
    private CancellationTokenSource?               _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartRecording))]
    [NotifyPropertyChangedFor(nameof(CanStopRecording))]
    private bool _isRecording;

    [ObservableProperty]
    private string _statusMessage = "Ready to record.";

    public ObservableCollection<string> EventLog { get; } = new();

    public bool CanStartRecording => !IsRecording;
    public bool CanStopRecording  =>  IsRecording;

    public RecordViewModel(IRecorderService recorder, ILogger<RecordViewModel> logger)
    {
        _recorder = recorder;
        _logger   = logger;

        _recorder.EventCaptured += OnEventCaptured;
    }

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        _cts = new CancellationTokenSource();
        await _recorder.StartAsync(_cts.Token).ConfigureAwait(false);
        IsRecording   = true;
        StatusMessage = "Recording… Press Ctrl+Alt+R to stop.";
        _logger.LogInformation("Recording started via UI.");
    }

    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        var actions = await _recorder.StopAsync().ConfigureAwait(false);
        IsRecording   = false;
        StatusMessage = $"Recording stopped. {actions.Count} actions captured.";
        _logger.LogInformation("Recording stopped. {Count} actions captured.", actions.Count);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void OnEventCaptured(object? sender, RecordedEventArgs e)
    {
        // Marshal to UI thread.
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            EventLog.Insert(0, $"[{e.Timestamp:HH:mm:ss.fff}] {e.Kind}: {e.Description}");
            if (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
        });
    }
}
