namespace SmartAuto.Domain.Models;

/// <summary>Result returned by <see cref="SmartAuto.Infrastructure.Engine.ExecutionEngine.ExecuteAsync"/>.</summary>
public sealed record ExecutionResult(
    bool Success,
    string? ErrorMessage,
    TimeSpan Elapsed,
    int ActionsExecuted,
    int ActionsFailed,
    string CorrelationId,
    string? StateDumpPath = null);

/// <summary>Progress notification emitted during script execution.</summary>
public sealed record ExecutionProgress(
    int TotalActions,
    int CompletedActions,
    string CurrentActionDescription,
    double PercentComplete);
