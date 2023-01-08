using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MusicDecrypto.Avalonia.ViewModels;

namespace MusicDecrypto.Avalonia.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        DataContext = new SettingsViewModel();

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
