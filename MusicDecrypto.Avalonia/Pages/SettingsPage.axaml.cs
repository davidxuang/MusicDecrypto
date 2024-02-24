using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicDecrypto.Avalonia.Helpers;
using MusicDecrypto.Avalonia.ViewModels;

namespace MusicDecrypto.Avalonia.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        DataContext = new SettingsViewModel();

        InitializeComponent();
    }

    private void OnLicenseClick(object? sender, RoutedEventArgs? eventArgs) => UrlHelper.OpenLink("https://www.gnu.org/licenses/agpl-3.0.html");
    private void OnGitHubClick(object? sender, RoutedEventArgs? eventArgs) => UrlHelper.OpenLink("https://github.com/davidxuang/MusicDecrypto");
}
