using Microsoft.Extensions.DependencyInjection;
using SmartAuto.Abstractions.Interfaces;
using SmartAuto.Application.Interfaces;
using SmartAuto.Application.Services;

namespace SmartAuto.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPlaybackEngine, ExecutionEngine>();
        services.AddScoped<IVariableEvaluator, DynamicExpressionEvaluator>();
        services.AddScoped<IScriptRepository, ScriptService>();
        return services;
    }
}
