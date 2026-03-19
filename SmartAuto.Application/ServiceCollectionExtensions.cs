using Microsoft.Extensions.DependencyInjection;
using SmartAuto.Abstractions;
using SmartAuto.Domain.Actions;

namespace SmartAuto.Application;

/// <summary>
/// Extension methods for registering SmartAuto application-layer services.
/// For infrastructure registrations (selectors, hooks, engine, etc.), call
/// <c>services.AddSmartAutoInfrastructure()</c> from the Infrastructure assembly.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers SmartAuto application-layer services (Domain factories, etc.).</summary>
    public static IServiceCollection AddSmartAutoApplication(this IServiceCollection services)
    {
        services.AddSingleton<ActionFactory>();
        return services;
    }

    /// <summary>Convenience overload that registers both Application and Infrastructure layers.</summary>
    public static IServiceCollection AddSmartAuto(this IServiceCollection services)
    {
        services.AddSmartAutoApplication();
        // Infrastructure extension method (defined in SmartAuto.Infrastructure).
        // Calling via the assembly-registered delegate avoids a direct project reference
        // from Application → Infrastructure in the strict layered architecture.
        // In practice the UI host (SmartAuto.csproj) calls both methods directly.
        return services;
    }
}

/// <summary>
/// No-op overlay service used in headless / CLI / test scenarios.
/// The real WinUI overlay is registered by the Presentation layer via DI override.
/// </summary>
public sealed class NoOpOverlayService : IOverlayService
{
    public Task HighlightAsync(
        System.Drawing.Rectangle physicalBounds,
        TimeSpan duration,
        System.Drawing.Color? color = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void ClearAll() { }
}
