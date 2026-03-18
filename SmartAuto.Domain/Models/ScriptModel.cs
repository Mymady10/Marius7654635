using System.Text.Json.Serialization;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Domain.Models;

/// <summary>
/// Root model for a SmartAuto automation script.
/// Persisted as versioned JSON (schema v1.1) with polymorphic action serialization.
/// Schema version allows automatic migration on load.
/// </summary>
public sealed class ScriptModel
{
    // ─── Schema versioning ───────────────────────────────────────────────────

    /// <summary>Schema version string.  Current supported: "1.1".</summary>
    public string SchemaVersion { get; set; } = "1.1";

    // ─── Metadata ────────────────────────────────────────────────────────────

    /// <summary>Stable script identifier (never changes between edits).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable script name.</summary>
    public string Name { get; set; } = "Untitled Script";

    /// <summary>Optional description of what this script does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Script author.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>UTC timestamp of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    // ─── Variables ───────────────────────────────────────────────────────────

    /// <summary>
    /// Named script variables.  Values support {{Var}} interpolation and
    /// DynamicExpresso simple math expressions.  Sensitive variables are stored
    /// encrypted (DPAPI) and serialized as base64 cipher-text.
    /// </summary>
    public Dictionary<string, ScriptVariable> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ─── Execution settings ──────────────────────────────────────────────────

    /// <summary>Default timeout per action if not overridden at action level.</summary>
    public TimeSpan DefaultActionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Default Polly retry count if not overridden at action level.</summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>Minimum inter-action delay in milliseconds (default 100 ms).</summary>
    public int MinInterActionDelayMs { get; set; } = 100;

    /// <summary>Whether to show the visual debug overlay before each action.</summary>
    public bool ShowDebugOverlay { get; set; } = false;

    /// <summary>Debug overlay display duration.</summary>
    public TimeSpan OverlayDuration { get; set; } = TimeSpan.FromSeconds(3);

    // ─── Actions ─────────────────────────────────────────────────────────────

    /// <summary>Ordered list of actions to execute.</summary>
    public List<ActionBase> Actions { get; set; } = new();

    // ─── Migration helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Migrates older schema versions to the current schema version.
    /// Called automatically when loading a script with an older schema version.
    /// </summary>
    public void MigrateIfNeeded()
    {
        if (SchemaVersion == "1.0")
        {
            // v1.0 → v1.1: Add default timeout/retry fields if missing.
            // (Fields have default values so migration is a no-op here.)
            SchemaVersion = "1.1";
        }
    }
}

/// <summary>A named script variable with optional DPAPI encryption for sensitive values.</summary>
public sealed class ScriptVariable
{
    /// <summary>Variable name (unique within the script, case-insensitive).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plaintext value for non-sensitive variables.
    /// For sensitive variables, this is base64-encoded DPAPI cipher-text.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether this variable contains sensitive data (masked in logs).</summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>Optional description / documentation for the variable.</summary>
    public string Description { get; set; } = string.Empty;
}
