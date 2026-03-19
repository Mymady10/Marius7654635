using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartAuto.ViewModels;

/// <summary>ViewModel for the main shell (NavigationView + AppBar).</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPageTitle = "Record";
}

/// <summary>ViewModel for the Script Editor page (Monaco editor hosted in WebView2).</summary>
public sealed partial class ScriptEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _scriptJson = "{}";

    [ObservableProperty]
    private bool _isDirty;
}

/// <summary>ViewModel for the Settings page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private int _defaultRetryCount = 3;

    [ObservableProperty]
    private int _defaultTimeoutSeconds = 10;

    [ObservableProperty]
    private bool _showDebugOverlay;

    [ObservableProperty]
    private bool _optInTelemetry;
}
