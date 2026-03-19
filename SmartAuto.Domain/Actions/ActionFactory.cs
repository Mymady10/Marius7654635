using System.Text.Json;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Domain.Actions;

/// <summary>
/// Factory for deserializing <see cref="ActionBase"/> instances from JSON
/// using exhaustive pattern matching on the "$type" discriminator.
/// Registered as a singleton in DI.
/// </summary>
public sealed class ActionFactory
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Deserializes a polymorphic action from a JSON string.
    /// The JSON must contain a "$type" property matching one of the registered discriminators.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the $type is unknown.</exception>
    public ActionBase Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$type", out var typeProp))
            throw new InvalidOperationException("JSON action is missing the required '$type' discriminator property.");

        var typeDiscriminator = typeProp.GetString() ?? string.Empty;

        // Exhaustive pattern matching on the discriminator value.
        ActionBase action = typeDiscriminator switch
        {
            "FindTextAndClick"  => root.Deserialize<FindTextAndClickAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize FindTextAndClickAction."),
            "FindColorAndClick" => root.Deserialize<FindColorAndClickAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize FindColorAndClickAction."),
            "FindImageAndClick" => root.Deserialize<FindImageAndClickAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize FindImageAndClickAction."),
            "TypeText"          => root.Deserialize<TypeTextAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize TypeTextAction."),
            "SendInput"         => root.Deserialize<SendInputAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize SendInputAction."),
            "WaitForCondition"  => root.Deserialize<WaitForConditionAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize WaitForConditionAction."),
            "IfCondition"       => root.Deserialize<IfConditionAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize IfConditionAction."),
            "Loop"              => root.Deserialize<LoopAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize LoopAction."),
            "Delay"             => root.Deserialize<DelayAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize DelayAction."),
            "MouseClick"        => root.Deserialize<MouseClickAction>(_options)
                                   ?? throw new InvalidOperationException("Failed to deserialize MouseClickAction."),
            _                   => throw new InvalidOperationException(
                                       $"Unknown action type discriminator: '{typeDiscriminator}'. " +
                                       "Ensure the action type is registered in ActionFactory and ActionBase [JsonDerivedType] attributes."),
        };

        return action;
    }

    /// <summary>
    /// Serializes an action to JSON using polymorphic type information.
    /// </summary>
    public string Serialize(ActionBase action)
        => JsonSerializer.Serialize<ActionBase>(action, _options);

    /// <summary>
    /// Deserializes a list of actions (used when loading a full script).
    /// </summary>
    public IReadOnlyList<ActionBase> DeserializeList(string json)
    {
        var actions = JsonSerializer.Deserialize<List<ActionBase>>(json, _options);
        return actions ?? [];
    }

    /// <summary>
    /// Serializes a list of actions to JSON.
    /// </summary>
    public string SerializeList(IReadOnlyList<ActionBase> actions)
        => JsonSerializer.Serialize<IReadOnlyList<ActionBase>>(actions, _options);
}
