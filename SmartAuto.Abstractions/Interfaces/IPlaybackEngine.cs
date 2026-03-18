using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

public interface IPlaybackEngine
{
    bool IsRunning { get; }
    Task<PlaybackResult> ExecuteAsync(ScriptDefinition script, IProgress<PlaybackProgress>? progress = null, CancellationToken ct = default);
    Task PauseAsync();
    Task ResumeAsync();
}
