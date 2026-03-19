namespace SmartAuto.Abstractions;

/// <summary>
/// Core contract for all automation actions.
/// Concrete types live in SmartAuto.Domain and carry [JsonDerivedType] attributes on ActionBase.
/// </summary>
public interface IAction
{
    /// <summary>Unique identifier for this action instance within a script.</summary>
    Guid Id { get; }

    /// <summary>Human-readable description shown in Script Editor.</summary>
    string Description { get; }

    /// <summary>Whether this action is enabled for execution.</summary>
    bool IsEnabled { get; }

    /// <summary>Timeout for this action (null = use global default).</summary>
    TimeSpan? Timeout { get; }

    /// <summary>Number of Polly retry attempts (null = use global default).</summary>
    int? RetryCount { get; }

    /// <summary>Action to take when this action fails: Stop, Continue, or jump to label.</summary>
    OnErrorBehavior OnError { get; }
}

/// <summary>
/// Defines how execution proceeds when an action encounters an error.
/// </summary>
public enum OnErrorBehavior
{
    /// <summary>Stop script execution immediately and report failure.</summary>
    Stop,

    /// <summary>Log the error but continue with next action.</summary>
    Continue,

    /// <summary>Skip to the next action in the else branch (for IfCondition).</summary>
    SkipToElse,
}
