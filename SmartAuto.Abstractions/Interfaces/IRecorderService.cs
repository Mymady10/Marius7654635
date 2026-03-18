using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

public interface IRecorderService : IAsyncDisposable
{
    bool IsRecording { get; }
    IObservable<RecordedAction> ActionStream { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
