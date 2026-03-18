using System.Text.Json.Serialization;

namespace SmartAuto.Domain.Actions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ClickAction), "click")]
[JsonDerivedType(typeof(TypeTextAction), "typeText")]
[JsonDerivedType(typeof(SendInputAction), "sendInput")]
[JsonDerivedType(typeof(FindTextAndClickAction), "findTextAndClick")]
[JsonDerivedType(typeof(FindColorAndClickAction), "findColorAndClick")]
[JsonDerivedType(typeof(FindImageAndClickAction), "findImageAndClick")]
[JsonDerivedType(typeof(WaitForConditionAction), "waitForCondition")]
[JsonDerivedType(typeof(DelayAction), "delay")]
[JsonDerivedType(typeof(IfConditionAction), "ifCondition")]
[JsonDerivedType(typeof(LoopAction), "loop")]
[JsonDerivedType(typeof(SetVariableAction), "setVariable")]
public abstract record ActionBase
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string? Label { get; init; }
    public bool IsEnabled { get; init; } = true;
    public int TimeoutMs { get; init; } = 30_000;
    public int RetryCount { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 500;
    public bool ContinueOnError { get; init; } = false;
    public string? OnErrorBranch { get; init; }

    public abstract string ActionType { get; }

    /// <summary>Human-readable description for the Script Editor UI.</summary>
    public virtual string Description => Label ?? ActionType;
}
