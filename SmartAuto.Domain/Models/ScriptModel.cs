using System.Text.Json;
using System.Text.Json.Serialization;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Domain.Models;

/// <summary>
/// Full persisted script model (v1.1 schema).
/// Includes metadata, variables, and polymorphic action list.
/// </summary>
public sealed class ScriptModel
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "https://smartauto.app/schemas/script/v1.1.json";

    public string SchemaVersion { get; init; } = "1.1";
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string Author { get; init; } = Environment.UserName;
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Variables { get; init; } = [];
    public List<ActionBase> Actions { get; init; } = [];
    public ScriptSettings Settings { get; init; } = new();

    public static ScriptModel Create(string name, string? description = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name,
        Description = description
    };

    public static JsonSerializerOptions CreateJsonOptions() => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class ScriptSettings
{
    public int DefaultDelayMs { get; init; } = 200;
    public bool HighlightElements { get; init; } = true;
    public int HighlightDurationMs { get; init; } = 3000;
    public bool TakeScreenshotOnError { get; init; } = true;
    public bool StopOnFirstError { get; init; } = false;
    public int PlaybackSpeedPercent { get; init; } = 100;
}
