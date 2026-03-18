using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

public interface IScriptRepository
{
    Task<ScriptDefinition?> LoadAsync(string path, CancellationToken ct = default);
    Task SaveAsync(ScriptDefinition script, string path, CancellationToken ct = default);
    IAsyncEnumerable<ScriptMetadata> GetAllAsync(string directory, CancellationToken ct = default);
}
