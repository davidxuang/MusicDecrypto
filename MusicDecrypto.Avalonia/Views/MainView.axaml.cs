using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.Core.ApplicationModel;
using FluentAvalonia.UI.Controls;
using MusicDecrypto.Avalonia.Pages;
using MusicDecrypto.Avalonia.ViewModels;

namespace MusicDecrypto.Avalonia.Views;

public partial class MainView : UserControl
{
    private Window? _parent;

    private Frame? _frameView;
    private FluentAvalonia.UI.Controls.Button? _openFilesButton;
    private FluentAvalonia.UI.Controls.Button? _settingsButton;

    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _parent = (Window)e.Root;
        _parent.Opened += OnParentWindowsOpened;

        _frameView = this.FindControl<Frame>("FrameView");
        _frameView.Navigate(typeof(HomePage));

        _openFilesButton = this.FindControl<FluentAvalonia.UI.Controls.Button>("OpenFilesButton");
        _openFilesButton.Click += OnOpenFilesButtonClickAsync;

        _settingsButton = this.FindControl<FluentAvalonia.UI.Controls.Button>("SettingsButton");
        _settingsButton.Click += OnSettingsButtonClick;
    }

    private void OnParentWindowsOpened(object? sender, EventArgs e)
    {
        if (sender is Window w) w.Opened -= OnParentWindowsOpened;

        if (sender is CoreWindow cw)
        {
            var titleBar = cw.TitleBar;
            if (titleBar != null)
            {
                titleBar.ExtendViewIntoTitleBar = true;
                titleBar.LayoutMetricsChanged += OnApplicationTitleBarLayoutMetricsChanged;

                if (this.FindControl<Grid>("TitleBarHost") is Grid g)
                {
                    cw.SetTitleBar(g);
                    g.Margin = new Thickness(titleBar.SystemOverlayLeftInset, 0, titleBar.SystemOverlayRightInset, 0);
                }
            }
        }
    }

    private void OnApplicationTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
    {
        if (this.FindControl<Grid>("TitleBarHost") is Grid g)
        {
            g.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);
        }
    }

    private async void OnOpenFilesButtonClickAsync(object? sender, RoutedEventArgs args)
    {
        OpenFileDialog dialog = new()
        {
            AllowMultiple = true,
            Directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        var paths = await dialog.ShowAsync(_parent!);
        if (paths != null && DataContext is MainViewModel vm)
        {
            foreach (var path in paths)
                vm.AddFile(path);
        }
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs args)
    {
        if (_frameView?.CurrentSourcePageType != typeof(SettingsPage))
            _frameView?.Navigate(typeof(SettingsPage));
    }
}
