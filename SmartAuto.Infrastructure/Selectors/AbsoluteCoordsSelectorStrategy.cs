using System.Drawing;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Strategy 4 (last resort): Absolute screen coordinates.
/// Always shows a user warning when used because coordinates are brittle
/// (change when window moves, resolution changes, DPI changes).
/// Confidence is always &lt; 50 when used as fallback.
/// </summary>
public sealed class AbsoluteCoordsSelectorStrategy : SelectorStrategyBase
{
    public override int    Priority => 4;
    public override string Name     => "AbsoluteCoords";

    public AbsoluteCoordsSelectorStrategy(
        ILogger<AbsoluteCoordsSelectorStrategy> logger,
        SelectorLruCache cache)
        : base(logger, cache) { }

    protected override ValueTask<SelectorResult?> FindCoreAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (criteria.AbsoluteCoords is null)
            return ValueTask.FromResult<SelectorResult?>(null);

        // Convert logical → physical coordinates.
        var physicalPoint = context.ToPhysical(criteria.AbsoluteCoords.Value);
        var bounds        = new Rectangle(physicalPoint.X - 2, physicalPoint.Y - 2, 5, 5);

        // Always log a prominent warning when falling back to absolute coordinates.
        Logger.LogWarning(
            "[AbsoluteCoords] Using absolute screen coordinates ({X},{Y}) as last resort. " +
            "This is brittle and will fail if the window moves or resolution changes. " +
            "Consider improving element detection in earlier strategies.",
            physicalPoint.X, physicalPoint.Y);

        // Confidence < 50 as per spec to flag the fallback to users.
        const int AbsoluteFallbackConfidence = 30;

        var result = new SelectorResult(
            PhysicalCenter: physicalPoint,
            PhysicalBounds: bounds,
            StrategyName:   Name,
            Confidence:     AbsoluteFallbackConfidence);

        return ValueTask.FromResult<SelectorResult?>(result);
    }
}
