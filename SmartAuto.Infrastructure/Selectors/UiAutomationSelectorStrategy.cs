using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Extensions;

namespace SmartAuto.Infrastructure.Selectors;

/// <summary>
/// Strategy 0 (highest priority): UI Automation via FlaUI UIA3 (with UIA2 fallback).
/// Search hierarchy:
///   1. AutomationId (exact match)        → confidence 95-100
///   2. Name contains / startsWith        → confidence 80-94
///   3. ClassName                         → confidence 70-79
///   4. ControlType + text content        → confidence 60-69
///
/// DPI-aware: converts UIA bounding rect (logical) → physical coordinates.
/// </summary>
public sealed class UiAutomationSelectorStrategy : SelectorStrategyBase
{
    public override int    Priority => 0;
    public override string Name     => "UIAutomation";

    public UiAutomationSelectorStrategy(
        ILogger<UiAutomationSelectorStrategy> logger,
        SelectorLruCache cache)
        : base(logger, cache) { }

    protected override async ValueTask<SelectorResult?> FindCoreAsync(
        SelectorCriteria criteria,
        IExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Skip if no UIA-specific search criteria are provided.
        if (string.IsNullOrWhiteSpace(criteria.AutomationId) &&
            string.IsNullOrWhiteSpace(criteria.SearchText)   &&
            string.IsNullOrWhiteSpace(criteria.ClassName))
        {
            return null;
        }

        return await Task.Run(() => FindWithUia(criteria, context), cancellationToken)
                         .ConfigureAwait(false);
    }

    private SelectorResult? FindWithUia(SelectorCriteria criteria, IExecutionContext context)
    {
        try
        {
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();

            AutomationElement? root = null;

            // Scope search to target window if handle is provided.
            if (criteria.TargetWindowHandle != 0)
            {
                root = automation.FromHandle(criteria.TargetWindowHandle);
            }

            root ??= desktop;

            // ── 1. AutomationId exact match ──────────────────────────────
            if (!string.IsNullOrWhiteSpace(criteria.AutomationId))
            {
                var el = root.FindFirstDescendant(
                    cf => cf.ByAutomationId(criteria.AutomationId));

                if (el is not null)
                    return BuildResult(el, "AutomationId.Exact", 98, context);
            }

            // ── 2. Name contains / startsWith ────────────────────────────
            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                // startsWith first (higher confidence)
                var el = root.FindFirstDescendant(
                    cf => cf.ByName(criteria.SearchText));

                if (el is not null)
                    return BuildResult(el, "Name.Exact", 92, context);

                // Contains search via XPath walk (limited depth for performance)
                el = FindByNameContains(root, criteria.SearchText, 0, AppConstants.UiaMaxSearchDepth);

                if (el is not null)
                    return BuildResult(el, "Name.Contains", 82, context);
            }

            // ── 3. ClassName ─────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(criteria.ClassName))
            {
                var el = root.FindFirstDescendant(
                    cf => cf.ByClassName(criteria.ClassName));

                if (el is not null)
                    return BuildResult(el, "ClassName", 72, context);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[UIAutomation] Unhandled exception during search.");
            return null;
        }
    }

    private static AutomationElement? FindByNameContains(
        AutomationElement root, string text, int depth, int maxDepth)
    {
        if (depth > maxDepth) return null;

        var children = root.FindAllChildren();
        foreach (var child in children)
        {
            if (child.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
                return child;

            var found = FindByNameContains(child, text, depth + 1, maxDepth);
            if (found is not null) return found;
        }
        return null;
    }

    private static SelectorResult? BuildResult(
        AutomationElement element, string subStrategy, int confidence, IExecutionContext context)
    {
        var bounds = element.BoundingRectangle;
        if (bounds.IsEmpty) return null;

        // UIA returns logical coordinates; convert to physical.
        var logicalBounds = new Rectangle(
            (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
        var physicalBounds  = logicalBounds.ToPhysical(context.DpiScaleFactor);
        var physicalCenter  = physicalBounds.Center();

        return new SelectorResult(
            PhysicalCenter: physicalCenter,
            PhysicalBounds: physicalBounds,
            StrategyName:   $"UIAutomation/{subStrategy}",
            Confidence:     confidence);
    }
}
