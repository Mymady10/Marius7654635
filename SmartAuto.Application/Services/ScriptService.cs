using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions.Interfaces;
using SmartAuto.Abstractions.Models;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Exceptions;
using SmartAuto.Domain.Models;

namespace SmartAuto.Application.Services;

public sealed class ScriptService : IScriptRepository
{
    private readonly ILogger<ScriptService> _logger;
    private static readonly JsonSerializerOptions _opts = ScriptModel.CreateJsonOptions();

    public ScriptService(ILogger<ScriptService> logger) => _logger = logger;

    public async Task<ScriptDefinition?> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            _logger.LogWarning("Script file not found: {Path}", path);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var model = await JsonSerializer.DeserializeAsync<ScriptModel>(stream, _opts, ct).ConfigureAwait(false)
                ?? throw new ScriptValidationException($"Script at '{path}' deserialized to null");

            _logger.LogInformation("Loaded script '{Name}' ({Actions} actions) from {Path}",
                model.Name, model.Actions.Count, path);

            return new ScriptDefinition
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description,
                Version = model.SchemaVersion,
                Created = model.Created,
                Modified = model.Modified,
                Variables = model.Variables,
                Actions = model.Actions.Cast<object>().ToList()
            };
        }
        catch (JsonException ex)
        {
            throw new ScriptValidationException($"Invalid script JSON at '{path}': {ex.Message}", [ex.Message]);
        }
    }

    public async Task SaveAsync(ScriptDefinition script, string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var model = new ScriptModel
        {
            Id = script.Id,
            Name = script.Name,
            Description = script.Description,
            SchemaVersion = AppConstants.ScriptSchemaVersion,
            Created = script.Created,
            Modified = DateTimeOffset.UtcNow,
            Variables = script.Variables,
            Actions = script.Actions.OfType<Domain.Actions.ActionBase>().ToList()
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, model, _opts, ct).ConfigureAwait(false);
        _logger.LogInformation("Saved script '{Name}' to {Path}", script.Name, path);
    }

    public async IAsyncEnumerable<ScriptMetadata> GetAllAsync(
        string directory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!Directory.Exists(directory)) yield break;

        foreach (var file in Directory.EnumerateFiles(directory, $"*{AppConstants.ScriptFileExtension}"))
        {
            ct.ThrowIfCancellationRequested();
            ScriptMetadata? meta = null;
            try
            {
                await using var stream = File.OpenRead(file);
                var model = await JsonSerializer.DeserializeAsync<ScriptModel>(stream, _opts, ct)
                    .ConfigureAwait(false);
                if (model is not null)
                {
                    meta = new ScriptMetadata
                    {
                        Name = model.Name,
                        FilePath = file,
                        Version = model.SchemaVersion,
                        Created = model.Created,
                        Modified = model.Modified,
                        ActionCount = model.Actions.Count
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read script metadata from {File}", file);
            }

            if (meta is not null) yield return meta;
        }
    }
}
