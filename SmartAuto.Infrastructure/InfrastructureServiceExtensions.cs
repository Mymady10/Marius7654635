using Microsoft.Extensions.DependencyInjection;
using SmartAuto.Abstractions;
using SmartAuto.Application;
using SmartAuto.Infrastructure.Engine;
using SmartAuto.Infrastructure.Hooks;
using SmartAuto.Infrastructure.Input;
using SmartAuto.Infrastructure.Selectors;
using SmartAuto.Infrastructure.Services;

namespace SmartAuto.Infrastructure;

/// <summary>
/// DI registration for all SmartAuto infrastructure services.
/// Call <c>services.AddSmartAutoInfrastructure()</c> from the application host
/// after <c>services.AddSmartAutoApplication()</c>.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all infrastructure services:
    /// hooks, selector strategies, execution engine, recorder, capture, overlay (no-op).
    /// </summary>
    public static IServiceCollection AddSmartAutoInfrastructure(this IServiceCollection services)
    {
        // ── Input ────────────────────────────────────────────────────────────
        services.AddSingleton<GlobalHookManager>();
        services.AddSingleton<SendInputService>();

        // ── Screen capture ───────────────────────────────────────────────────
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();

        // ── Selector strategies (registered in priority order) ───────────────
        services.AddSingleton<SelectorLruCache>();
        services.AddSingleton<ISelectorStrategy, UiAutomationSelectorStrategy>();
        services.AddSingleton<ISelectorStrategy, OcrSelectorStrategy>();
        services.AddSingleton<ISelectorStrategy, ColorMatchSelectorStrategy>();
        services.AddSingleton<ISelectorStrategy, ImageTemplateSelectorStrategy>();
        services.AddSingleton<ISelectorStrategy, AbsoluteCoordsSelectorStrategy>();
        services.AddSingleton<SelectorEngine>();

        // ── Execution engine ─────────────────────────────────────────────────
        services.AddSingleton<ExecutionEngine>();

        // ── Recorder ─────────────────────────────────────────────────────────
        services.AddSingleton<IRecorderService, RecorderService>();

        // ── Overlay (no-op; WinUI layer overrides with real overlay) ─────────
        services.AddSingleton<IOverlayService, NoOpOverlayService>();

        return services;
    }
}
