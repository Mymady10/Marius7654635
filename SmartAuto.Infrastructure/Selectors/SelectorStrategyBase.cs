using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Base class for all selector strategies.
/// Provides LRU caching, criteria hashing, and confidence-score helpers.
/// </summary>
public abstract class SelectorStrategyBase : ISelectorStrategy
{
    protected readonly ILogger Logger;
    protected readonly SelectorLruCache Cache;

    protected SelectorStrategyBase(ILogger logger, SelectorLruCache cache)
    {
        Logger = logger;
        Cache  = cache;
    }

    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public async ValueTask<SelectorResult?> TryFindAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var hash = ComputeCriteriaHash(criteria);

        // 1. Check LRU cache first.
        if (Cache.TryGet(criteria.TargetWindowHandle, Name, hash, out var cached) && cached is not null)
        {
            Logger.LogDebug("[{Strategy}] Cache hit (confidence={Conf}).", Name, cached.Confidence);
            return cached;
        }

        // 2. Execute the concrete strategy implementation.
        SelectorResult? result = null;
        try
        {
            result = await FindCoreAsync(criteria, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{Strategy}] Strategy threw an exception.", Name);
            return null;
        }

        // 3. Store successful result in cache.
        if (result is not null)
        {
            Cache.Set(criteria.TargetWindowHandle, Name, hash, result);
            Logger.LogDebug("[{Strategy}] Found element. Center={Center}, Confidence={Conf}.",
                Name, result.PhysicalCenter, result.Confidence);
        }
        else
        {
            Logger.LogDebug("[{Strategy}] Element not found.", Name);
        }

        return result;
    }

    /// <summary>Concrete find logic implemented by each strategy subclass.</summary>
    protected abstract ValueTask<SelectorResult?> FindCoreAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Computes a deterministic hash of a <see cref="SelectorCriteria"/> for cache keying.</summary>
    protected static string ComputeCriteriaHash(SelectorCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.Append(criteria.SearchText    ?? string.Empty);
        sb.Append('|');
        sb.Append(criteria.AutomationId  ?? string.Empty);
        sb.Append('|');
        sb.Append(criteria.ClassName     ?? string.Empty);
        sb.Append('|');
        sb.Append(criteria.TargetColor?.ToArgb().ToString() ?? string.Empty);
        sb.Append('|');
        sb.Append(criteria.SearchRegion?.ToString() ?? string.Empty);

        var bytes  = Encoding.UTF8.GetBytes(sb.ToString());
        var hash   = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Calculates a weighted confidence score in the range 0-100.
    /// </summary>
    protected static int ComputeConfidence(double rawScore, double threshold,
        bool hasAnchor = false, bool exactMatch = false)
    {
        // rawScore is 0.0-1.0 (e.g. template-match correlation).
        // Map to 0-100 with weight adjustments.
        var baseScore = Math.Clamp(rawScore * 100.0, 0.0, 100.0);

        if (exactMatch) baseScore = Math.Max(baseScore, 95.0);
        if (hasAnchor)  baseScore = Math.Min(baseScore + 5.0, 100.0);

        return (int)Math.Round(baseScore);
    }
}
