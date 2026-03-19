namespace SmartAuto.Abstractions;

/// <summary>
/// Read-only view of execution state passed to action executors and selector strategies.
/// </summary>
public interface IExecutionContext
{
    /// <summary>Correlation ID for structured logging (one per script run).</summary>
    string CorrelationId { get; }

    /// <summary>CancellationToken for the entire execution run.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Tries to resolve a script variable by name; returns null if not found.</summary>
    string? GetVariable(string name);

    /// <summary>Sets or updates a runtime variable.</summary>
    void SetVariable(string name, string value);

    /// <summary>DPI scale factor for the primary monitor (logical → physical).</summary>
    double DpiScaleFactor { get; }

    /// <summary>
    /// Converts a logical-pixel coordinate to a physical-pixel coordinate
    /// using the per-monitor DPI of the monitor that contains the point.
    /// </summary>
    System.Drawing.Point ToPhysical(System.Drawing.Point logical);

    /// <summary>
    /// Converts a physical-pixel coordinate to a logical-pixel coordinate.
    /// </summary>
    System.Drawing.Point ToLogical(System.Drawing.Point physical);
}
