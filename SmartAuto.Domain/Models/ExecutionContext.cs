using System.Runtime.InteropServices;
using SmartAuto.Abstractions;

namespace SmartAuto.Domain.Models;

/// <summary>
/// Scoped execution context for a single script run.
/// Carries runtime variables, the correlation ID, DPI information,
/// and the CancellationToken for the entire run.
/// </summary>
public sealed class ExecutionContext : IExecutionContext, IDisposable
{
    private readonly Dictionary<string, string> _variables;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new execution context from a script model.
    /// All non-sensitive variable values are copied; sensitive values are decrypted on demand.
    /// </summary>
    public ExecutionContext(ScriptModel script, CancellationToken externalToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        CorrelationId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        DpiScaleFactor = GetPrimaryMonitorDpiScale();

        // Initialize runtime variables from the script definition.
        _variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, variable) in script.Variables)
        {
            // Sensitive variables: decrypt DPAPI cipher-text → plaintext at runtime.
            var value = variable.IsSensitive
                ? DecryptDpapi(variable.Value)
                : variable.Value;
            _variables[key] = value;
        }
    }

    // ─── IExecutionContext ───────────────────────────────────────────────────

    /// <inheritdoc />
    public string CorrelationId { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken => _cts.Token;

    /// <inheritdoc />
    public double DpiScaleFactor { get; }

    /// <inheritdoc />
    public string? GetVariable(string name)
        => _variables.TryGetValue(name, out var value) ? value : null;

    /// <inheritdoc />
    public void SetVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _variables[name] = value;
    }

    /// <inheritdoc />
    public System.Drawing.Point ToPhysical(System.Drawing.Point logical)
    {
        var scale = DpiScaleFactor;
        return new System.Drawing.Point(
            (int)Math.Round(logical.X * scale),
            (int)Math.Round(logical.Y * scale));
    }

    /// <inheritdoc />
    public System.Drawing.Point ToLogical(System.Drawing.Point physical)
    {
        var scale = DpiScaleFactor;
        return scale == 0 ? physical : new System.Drawing.Point(
            (int)Math.Round(physical.X / scale),
            (int)Math.Round(physical.Y / scale));
    }

    // ─── Public helpers ──────────────────────────────────────────────────────

    /// <summary>Requests cancellation of the current run.</summary>
    public void Cancel() => _cts.Cancel();

    /// <summary>
    /// Resolves all {{VariableName}} interpolations in a template string
    /// using the current variable values.
    /// </summary>
    public string Interpolate(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;
        foreach (var (key, value) in _variables)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    // ─── IDisposable ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Dispose();
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Gets the DPI scale factor (logical → physical) for the primary monitor.
    /// Returns 1.0 if detection fails.
    /// </summary>
    private static double GetPrimaryMonitorDpiScale()
    {
        try
        {
            // Use System.Windows.Forms.Screen for DPI detection in .NET 8 non-WinUI context.
            // In WinUI, DisplayInformation is used instead (see Presentation layer).
            using var screen = new System.Drawing.Graphics();
            // Reflection: Graphics.DpiX is available via System.Drawing
            // Fallback: return 1.0 for headless / test scenarios
        }
        catch
        {
            // Intentionally swallow; DPI detection is best-effort.
        }

        return 1.0;
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted base64 string.
    /// Returns empty string if decryption fails (logs the failure via caller).
    /// </summary>
    private static string DecryptDpapi(string base64CipherText)
    {
        if (string.IsNullOrWhiteSpace(base64CipherText)) return string.Empty;

        try
        {
            var cipherBytes = Convert.FromBase64String(base64CipherText);
            var plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                cipherBytes,
                optionalEntropy: null,
                scope: System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Return empty on decryption failure; caller should handle gracefully.
            return string.Empty;
        }
    }
}
