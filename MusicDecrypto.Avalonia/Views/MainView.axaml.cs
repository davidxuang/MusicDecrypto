using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MusicDecrypto.Avalonia.Controls;
using MusicDecrypto.Avalonia.Pages;
using MusicDecrypto.Avalonia.ViewModels;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.Views;

public partial class MainView : UserControl
{
    private Window? _window;

    private Frame? _frameView;
    private Button? _openFilesButton;
    private Button? _settingsButton;

    private static readonly SemaphoreSlim _dialogLock = new(1);

    static MainView()
    {
        DecryptoBase.OnRequestMatch += OnRequestMatchAsync;
    }

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _window = e.Root as Window;

        _frameView = this.FindControl<Frame>("FrameView");
        _frameView?.Navigate(typeof(HomePage));
        _openFilesButton = this.FindControl<Button>("OpenFilesButton");
        if (_openFilesButton != null) _openFilesButton.Click += OnOpenFilesButtonClickAsync;
        _settingsButton = this.FindControl<Button>("SettingsButton");
        if (_settingsButton != null) _settingsButton.Click += OnSettingsButtonClick;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (VisualRoot is AppWindow window)
        {
            TitleBarHost.ColumnDefinitions[4].Width = new GridLength(window.TitleBar.RightInset, GridUnitType.Pixel);
        }
    }

    private async void OnOpenFilesButtonClickAsync(object? sender, RoutedEventArgs args)
    {
        var storage = _window?.StorageProvider;
        if (storage?.CanOpen == true && DataContext is MainViewModel vm)
        {
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = true,
                SuggestedStartLocation = await storage.TryGetWellKnownFolderAsync(WellKnownFolder.Music),
            };
            foreach (var file in await storage.OpenFilePickerAsync(options))
            {
                vm.AddFile(file);
            }
        }
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs args)
    {
        if (_frameView?.CurrentSourcePageType != typeof(SettingsPage))
            _frameView?.Navigate(typeof(SettingsPage));
    }

    private static async ValueTask<bool> OnRequestMatchAsync(
        DecryptoBase sender,
        string? message,
        (DecryptoBase.MatchInfo, DecryptoBase.MatchInfo) properties)
    {
        await _dialogLock.WaitAsync();
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ContentDialog()
                {
                    Title = message,
                    PrimaryButtonText = "Confirm",
                    CloseButtonText = "Cancel",
                    Content = new MatchDialogContent()
                    {
                        DataContext = new MatchViewModel(properties.Item1, properties.Item2)
                    }
                };

                return (await dialog.ShowAsync()) == ContentDialogResult.Primary;
            });
        }
        catch
        {
            return false;
        }
        finally
        {
            _dialogLock.Release();
        }
    }
}
