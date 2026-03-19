using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Orchestrates the strict hierarchical fallback across all registered <see cref="ISelectorStrategy"/> implementations.
/// Strategies are tried in ascending <see cref="ISelectorStrategy.Priority"/> order.
/// Stops at the first result meeting the minimum confidence threshold.
/// </summary>
public sealed class SelectorEngine
{
    private readonly IReadOnlyList<ISelectorStrategy> _strategies;
    private readonly ILogger<SelectorEngine> _logger;

    public SelectorEngine(
        IEnumerable<ISelectorStrategy> strategies,
        ILogger<SelectorEngine> logger)
    {
        _logger     = logger;
        // Sort by priority ascending (0 = highest priority).
        _strategies = strategies.OrderBy(s => s.Priority).ToList();
    }

    /// <summary>
    /// Tries all strategies in priority order until one succeeds.
    /// Returns the first result with confidence ≥ <paramref name="minConfidence"/>.
    /// Returns null if no strategy succeeds.
    /// </summary>
    public async ValueTask<SelectorResult?> FindElementAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        int minConfidence = AppConstants.DefaultMinConfidence,
        CancellationToken cancellationToken = default)
    {
        var strategyResults = new List<(string Name, string Outcome)>();

        foreach (var strategy in _strategies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("[SelectorEngine] Trying strategy {Strategy} (priority {Pri}).",
                strategy.Name, strategy.Priority);

            var result = await strategy.TryFindAsync(criteria, context, cancellationToken)
                                       .ConfigureAwait(false);

            if (result is null)
            {
                strategyResults.Add((strategy.Name, "NotFound"));
                continue;
            }

            if (result.Confidence < minConfidence)
            {
                strategyResults.Add((strategy.Name, $"LowConfidence({result.Confidence})"));
                _logger.LogDebug("[SelectorEngine] {Strategy} returned confidence {Conf} < min {Min}; skipping.",
                    strategy.Name, result.Confidence, minConfidence);
                continue;
            }

            strategyResults.Add((strategy.Name, $"Found(confidence={result.Confidence})"));
            _logger.LogInformation(
                "[SelectorEngine] Element found by {Strategy} at ({X},{Y}), confidence={Conf}.",
                result.StrategyName, result.PhysicalCenter.X, result.PhysicalCenter.Y, result.Confidence);

            return result;
        }

        _logger.LogWarning(
            "[SelectorEngine] All {Count} strategies exhausted without finding the element. " +
            "Attempts: {Attempts}",
            _strategies.Count,
            string.Join(", ", strategyResults.Select(r => $"{r.Name}:{r.Outcome}")));

        return null;
    }
}
