using Microsoft.UI.Xaml.Controls;
using SmartAuto.ViewModels;

namespace SmartAuto.Views;

/// <summary>Record page code-behind.</summary>
public sealed partial class RecordPage : Page
{
    public RecordViewModel ViewModel { get; }

    public RecordPage()
    {
        ViewModel = App.Services.GetRequiredService<RecordViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }
}
