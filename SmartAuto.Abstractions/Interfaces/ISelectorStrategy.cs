using SmartAuto.Abstractions.Models;

namespace SmartAuto.Abstractions.Interfaces;

/// <summary>Defines a selector strategy that can locate a UI element on screen.</summary>
public interface ISelectorStrategy
{
    /// <summary>Priority order – lower runs first (0 = highest).</summary>
    int Priority { get; }

    /// <summary>Human-readable strategy name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Attempt to find element matching <paramref name="selector"/>.
    /// Returns null when the element cannot be found by this strategy.
    /// </summary>
    Task<SelectorResult?> FindAsync(ElementSelector selector, CancellationToken ct = default);
}
