using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Events;
using SmartAuto.Application;
using SmartAuto.Common.Constants;
using SmartAuto.Infrastructure;

namespace SmartAuto;

/// <summary>
/// Application entry point.
/// Bootstraps the .NET Generic Host, configures Serilog, registers all SmartAuto services,
/// and launches the main WinUI 3 window.
/// Single-instance enforcement via named Mutex.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;

    public App()
    {
        // Single-instance check using named Mutex.
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: AppConstants.MutexName,
            out bool isNewInstance);

        if (!isNewInstance)
        {
            // Another instance is already running; bring it to foreground and exit.
            Current.Exit();
            return;
        }

        InitializeComponent();
    }

    /// <summary>Exposes the DI service provider to ViewModels and other components.</summary>
    public static IServiceProvider Services => ((App)Current)._host!.Services;

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Configure Serilog.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppConstants.AppName, "Logs", AppConstants.LogFileName),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console()
            .CreateLogger();

        // Build the host.
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddSmartAutoApplication();
                services.AddSmartAutoInfrastructure();
                // Register ViewModels.
                services.AddSingleton<ViewModels.MainViewModel>();
                services.AddSingleton<ViewModels.RecordViewModel>();
                services.AddSingleton<ViewModels.ScriptEditorViewModel>();
                services.AddSingleton<ViewModels.RunDebugViewModel>();
                services.AddSingleton<ViewModels.SettingsViewModel>();
            })
            .Build();

        await _host.StartAsync().ConfigureAwait(false);

        // Launch main window on the UI thread.
        var window = new MainWindow();
        window.Activate();
    }

    /// <inheritdoc />
    protected override void OnLaunchActivated(LaunchActivatedEventArgs args)
    {
        base.OnLaunchActivated(args);
    }
}
