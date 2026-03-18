using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartAuto.ViewModels;

namespace SmartAuto;

/// <summary>
/// Main application window with NavigationView (left pane) + top AppBar.
/// Applies Mica backdrop on Windows 11 with Acrylic fallback on Windows 10.
/// </summary>
public sealed partial class MainWindow : Window
{
    private MicaController?    _micaController;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration _backdropConfig = new();

    public MainWindow()
    {
        InitializeComponent();

        Title = "SmartAuto – Desktop Automation";
        ExtendsContentIntoTitleBar = true;

        SetupBackdrop();
    }

    private void SetupBackdrop()
    {
        // Try Mica first (Windows 11 only).
        if (MicaController.IsSupported())
        {
            _micaController = new MicaController();
            _backdropConfig = new SystemBackdropConfiguration
            {
                IsInputActive = true,
            };
            _micaController.AddSystemBackdropTarget(
                this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            _micaController.SetSystemBackdropConfiguration(_backdropConfig);
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            // Acrylic fallback for Windows 10.
            _acrylicController = new DesktopAcrylicController();
            _backdropConfig = new SystemBackdropConfiguration
            {
                IsInputActive = true,
            };
            _acrylicController.AddSystemBackdropTarget(
                this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString() ?? string.Empty;
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        var pageType = tag switch
        {
            "Record"       => typeof(Views.RecordPage),
            "ScriptEditor" => typeof(Views.ScriptEditorPage),
            "RunDebug"     => typeof(Views.RunDebugPage),
            "Settings"     => typeof(Views.SettingsPage),
            "Library"      => typeof(Views.LibraryPage),
            _              => typeof(Views.RecordPage),
        };

        ContentFrame.Navigate(pageType);
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        _backdropConfig.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
    }
}
