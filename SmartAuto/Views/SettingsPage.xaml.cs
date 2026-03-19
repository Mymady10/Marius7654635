using Microsoft.UI.Xaml.Controls;
using SmartAuto.ViewModels;
namespace SmartAuto.Views;
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }
}
