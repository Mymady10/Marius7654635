using System.Text.Json;
using System.Text.Json.Serialization;
using SmartAuto.Domain.Actions;
using SmartAuto.Domain.Models;

namespace SmartAuto.Domain.Factories;

public static class ActionFactory
{
    private static readonly JsonSerializerOptions _opts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Deserializes a JSON element to a strongly-typed ActionBase.</summary>
    public static ActionBase? FromJson(JsonElement element)
    {
        if (!element.TryGetProperty("$type", out var typeEl)) return null;
        var type = typeEl.GetString() ?? string.Empty;

        return type switch
        {
            "click"              => element.Deserialize<ClickAction>(_opts),
            "typeText"           => element.Deserialize<TypeTextAction>(_opts),
            "sendInput"          => element.Deserialize<SendInputAction>(_opts),
            "findTextAndClick"   => element.Deserialize<FindTextAndClickAction>(_opts),
            "findColorAndClick"  => element.Deserialize<FindColorAndClickAction>(_opts),
            "findImageAndClick"  => element.Deserialize<FindImageAndClickAction>(_opts),
            "waitForCondition"   => element.Deserialize<WaitForConditionAction>(_opts),
            "delay"              => element.Deserialize<DelayAction>(_opts),
            "ifCondition"        => element.Deserialize<IfConditionAction>(_opts),
            "loop"               => element.Deserialize<LoopAction>(_opts),
            "setVariable"        => element.Deserialize<SetVariableAction>(_opts),
            _                    => null
        };
    }

    /// <summary>Returns a string descriptor of an action for logging.</summary>
    public static string Describe(ActionBase action) => action switch
    {
        ClickAction c            => $"Click at ({c.Position.X},{c.Position.Y}){(c.IsDoubleClick ? " [double]" : "")}",
        TypeTextAction t         => t.IsSensitive ? "TypeText: <MASKED>" : $"TypeText: \"{t.Text}\"",
        SendInputAction s        => $"SendInput: {(s.Ctrl ? "Ctrl+" : "")}{(s.Alt ? "Alt+" : "")}{(s.Shift ? "Shift+" : "")}{s.Keys}",
        FindTextAndClickAction f => $"FindTextAndClick: \"{f.TextToFind}\"",
        FindColorAndClickAction colorClick => $"FindColorAndClick: RGB({colorClick.R},{colorClick.G},{colorClick.B})±{colorClick.Tolerance}",
        FindImageAndClickAction  => "FindImageAndClick",
        WaitForConditionAction w => $"WaitForCondition: {w.Condition}",
        DelayAction d            => $"Delay: {d.DelayMs}ms",
        IfConditionAction i      => $"If: {i.Condition}",
        LoopAction l             => l.FixedCount.HasValue ? $"Loop {l.FixedCount}x" : $"Loop while: {l.WhileCondition}",
        SetVariableAction sv     => $"Set {sv.VariableName} = {sv.Expression}",
        _                        => action.ActionType
    };
}
