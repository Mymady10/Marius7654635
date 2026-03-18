namespace SmartAuto.Domain.Exceptions;

/// <summary>Base class for all SmartAuto-specific exceptions.</summary>
public abstract class SmartAutoException : Exception
{
    protected SmartAutoException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>Thrown when all selector strategies fail to locate an element.</summary>
public sealed class ElementNotFoundException : SmartAutoException
{
    public string StrategyAttempted { get; }
    public int AttemptsCount { get; }

    public ElementNotFoundException(string message, string strategyAttempted, int attemptsCount, Exception? inner = null)
        : base(message, inner)
    {
        StrategyAttempted = strategyAttempted;
        AttemptsCount = attemptsCount;
    }
}

/// <summary>Thrown when a selector result's confidence is below the required minimum.</summary>
public sealed class LowConfidenceException : SmartAutoException
{
    public int ActualConfidence { get; }
    public int RequiredConfidence { get; }

    public LowConfidenceException(int actual, int required, string? context = null)
        : base($"Selector confidence {actual} is below the required minimum {required}. {context}".TrimEnd())
    {
        ActualConfidence = actual;
        RequiredConfidence = required;
    }
}

/// <summary>Thrown when a script action times out.</summary>
public sealed class ActionTimeoutException : SmartAutoException
{
    public TimeSpan Elapsed { get; }
    public TimeSpan Limit { get; }

    public ActionTimeoutException(TimeSpan elapsed, TimeSpan limit, string actionDescription)
        : base($"Action '{actionDescription}' timed out after {elapsed.TotalSeconds:F1}s (limit: {limit.TotalSeconds:F1}s).")
    {
        Elapsed = elapsed;
        Limit = limit;
    }
}

/// <summary>Thrown when script execution is cancelled by the user.</summary>
public sealed class ExecutionCancelledException : SmartAutoException
{
    public ExecutionCancelledException(string? message = null)
        : base(message ?? "Script execution was cancelled by the user.") { }
}

/// <summary>Thrown when expression evaluation fails (DynamicExpresso).</summary>
public sealed class ExpressionEvaluationException : SmartAutoException
{
    public string Expression { get; }

    public ExpressionEvaluationException(string expression, Exception? inner = null)
        : base($"Failed to evaluate expression: '{expression}'.", inner)
    {
        Expression = expression;
    }
}

/// <summary>Thrown when a script schema version cannot be migrated.</summary>
public sealed class ScriptMigrationException : SmartAutoException
{
    public string SchemaVersion { get; }

    public ScriptMigrationException(string schemaVersion, Exception? inner = null)
        : base($"Cannot migrate script from schema version '{schemaVersion}'.", inner)
    {
        SchemaVersion = schemaVersion;
    }
}
