using Microsoft.UI.Xaml.Controls;
using SmartAuto.ViewModels;

namespace SmartAuto.Views;

public sealed partial class RunDebugPage : Page
{
    public RunDebugViewModel ViewModel { get; }

    public RunDebugPage()
    {
        ViewModel = App.Services.GetRequiredService<RunDebugViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }
}
