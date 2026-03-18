using SmartAuto.Abstractions.Models;

namespace SmartAuto.Domain.Actions;

public record ClickAction : ActionBase
{
    public override string ActionType => "Click";
    public required ScreenPoint Position { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
    public bool IsDoubleClick { get; init; }
    public ElementSelector? Selector { get; init; }
}

public record TypeTextAction : ActionBase
{
    public override string ActionType => "TypeText";
    public required string Text { get; init; }
    public bool IsSensitive { get; init; }
    public int CharDelayMs { get; init; } = 0;
    public override string Description => IsSensitive ? $"{Label ?? ActionType}: <MASKED>" : $"{Label ?? ActionType}: \"{Text}\"";
}

public record SendInputAction : ActionBase
{
    public override string ActionType => "SendInput";
    public required string Keys { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool Win { get; init; }
}

public record FindTextAndClickAction : ActionBase
{
    public override string ActionType => "FindTextAndClick";
    public required string TextToFind { get; init; }
    public MatchMode MatchMode { get; init; } = MatchMode.Contains;
    public bool CaseSensitive { get; init; }
    public ScreenRect? SearchRegion { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
    public string? OcrLanguage { get; init; } = "eng";
    public override string Description => $"{Label ?? ActionType}: \"{TextToFind}\"";
}

public record FindColorAndClickAction : ActionBase
{
    public override string ActionType => "FindColorAndClick";
    public required byte R { get; init; }
    public required byte G { get; init; }
    public required byte B { get; init; }
    public byte Tolerance { get; init; } = 15;
    public ScreenRect? SearchRegion { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
    public override string Description => $"{Label ?? ActionType}: RGB({R},{G},{B})±{Tolerance}";
}

public record FindImageAndClickAction : ActionBase
{
    public override string ActionType => "FindImageAndClick";
    public required string ImageBase64 { get; init; }
    public double MatchThreshold { get; init; } = 0.95;
    public ScreenRect? SearchRegion { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
}

public record WaitForConditionAction : ActionBase
{
    public override string ActionType => "WaitForCondition";
    public required string Condition { get; init; }
    public int PollIntervalMs { get; init; } = 500;
    public override string Description => $"{Label ?? ActionType}: {Condition}";
}

public record DelayAction : ActionBase
{
    public override string ActionType => "Delay";
    public required int DelayMs { get; init; }
    public override string Description => $"{Label ?? ActionType}: {DelayMs}ms";
}

public record IfConditionAction : ActionBase
{
    public override string ActionType => "IfCondition";
    public required string Condition { get; init; }
    public List<ActionBase> ThenActions { get; init; } = [];
    public List<ActionBase> ElseActions { get; init; } = [];
}

public record LoopAction : ActionBase
{
    public override string ActionType => "Loop";
    public int? FixedCount { get; init; }
    public string? WhileCondition { get; init; }
    public int MaxIterations { get; init; } = 100;
    public List<ActionBase> Body { get; init; } = [];
}

public record SetVariableAction : ActionBase
{
    public override string ActionType => "SetVariable";
    public required string VariableName { get; init; }
    public required string Expression { get; init; }
    public override string Description => $"{Label ?? ActionType}: {VariableName} = {Expression}";
}

public enum MatchMode { Exact, Contains, StartsWith, Regex }
