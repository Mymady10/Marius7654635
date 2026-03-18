using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmartAuto.Domain.Models;
using SmartAuto.Infrastructure.Engine;

namespace SmartAuto.ViewModels;

/// <summary>
/// ViewModel for the Run &amp; Debug page.
/// Manages script execution lifecycle, progress tracking, and real-time log output.
/// </summary>
public sealed partial class RunDebugViewModel : ObservableObject
{
    private readonly ExecutionEngine           _engine;
    private readonly ILogger<RunDebugViewModel> _logger;
    private CancellationTokenSource?           _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    private bool _isRunning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private string _logOutput = string.Empty;

    public bool CanRun  => !IsRunning;
    public bool CanStop =>  IsRunning;

    public ScriptModel? CurrentScript { get; set; }

    public RunDebugViewModel(ExecutionEngine engine, ILogger<RunDebugViewModel> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunScriptAsync()
    {
        if (CurrentScript is null)
        {
            StatusMessage = "No script loaded. Open a script in the Script Editor first.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning     = true;
        ProgressPercent = 0;
        StatusMessage = "Running…";

        var progress = new Progress<ExecutionProgress>(p =>
        {
            ProgressPercent = p.PercentComplete;
            StatusMessage   = p.CurrentActionDescription;
        });

        try
        {
            var result = await Task.Run(
                () => _engine.ExecuteAsync(CurrentScript!, progress, _cts.Token),
                _cts.Token).ConfigureAwait(false);

            StatusMessage = result.Success
                ? $"Completed in {result.Elapsed.TotalSeconds:F2}s. " +
                  $"{result.ActionsExecuted} actions executed."
                : $"Failed: {result.ErrorMessage}";

            AppendLog($"[{DateTimeOffset.Now:HH:mm:ss}] {StatusMessage}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError(ex, "Script execution failed.");
        }
        finally
        {
            IsRunning       = false;
            ProgressPercent = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopScript()
    {
        _cts?.Cancel();
        _engine.Pause();
        StatusMessage = "Stopping…";
    }

    [RelayCommand]
    private void PauseOrResume()
    {
        if (_engine.IsRunning)
        {
            _engine.Pause();
            StatusMessage = "Paused.";
        }
        else
        {
            _engine.Resume();
        }
    }

    private void AppendLog(string line)
    {
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            LogOutput += line + Environment.NewLine;
        });
    }
}
