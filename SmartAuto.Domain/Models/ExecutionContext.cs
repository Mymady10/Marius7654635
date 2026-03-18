namespace SmartAuto.Domain.Models;

/// <summary>
/// Holds runtime state for a single script execution session.
/// Scoped per-execution; never shared across parallel runs.
/// </summary>
public sealed class ExecutionContext : IDisposable
{
    private bool _disposed;

    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
    public CancellationTokenSource CancellationSource { get; } = new();
    public CancellationToken CancellationToken => CancellationSource.Token;

    private readonly Dictionary<string, object?> _variables = [];
    private readonly object _lock = new();

    public void SetVariable(string name, object? value)
    {
        lock (_lock) { _variables[name] = value; }
    }

    public bool TryGetVariable(string name, out object? value)
    {
        lock (_lock) { return _variables.TryGetValue(name, out value); }
    }

    public IReadOnlyDictionary<string, object?> GetSnapshot()
    {
        lock (_lock) { return new Dictionary<string, object?>(_variables); }
    }

    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;
    public bool IsPaused { get; private set; }

    // Breakpoints: set of action IDs to pause at
    public HashSet<string> Breakpoints { get; } = [];

    public List<ExecutionLogEntry> Log { get; } = [];

    public void AddLog(string message, ExecutionLogLevel level = ExecutionLogLevel.Information, string? actionId = null) =>
        Log.Add(new ExecutionLogEntry(DateTimeOffset.UtcNow, level, message, actionId));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancellationSource.Dispose();
    }
}

public sealed record ExecutionLogEntry(DateTimeOffset Timestamp, ExecutionLogLevel Level, string Message, string? ActionId);

public enum ExecutionLogLevel { Debug, Information, Warning, Error }
