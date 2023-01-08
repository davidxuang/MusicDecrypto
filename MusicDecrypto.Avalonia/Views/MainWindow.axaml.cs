using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media;
using MusicDecrypto.Avalonia.ViewModels;

namespace MusicDecrypto.Avalonia.Views;

public partial class MainWindow : CoreWindow
{
    public MainWindow()
    {
        DataContext = new MainViewModel();

        InitializeComponent();

        SetupDnd();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupDnd()
    {
        void DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects &= DragDropEffects.Copy;

            if (!e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.None;
        }

        void Drop(object? sender, DragEventArgs e)
        {
            e.DragEffects &= DragDropEffects.Copy;

            if (e.Data.Contains(DataFormats.FileNames))
            {
                if (DataContext is MainViewModel vm)
                {
                    foreach (var path in e.Data.GetFileNames()!)
                    {
                        if (Directory.Exists(path))
                        {
                            Array.ForEach(
                                Directory.GetFiles(path, "*", SearchOption.AllDirectories),
                                f => vm.AddFile(f));
                        }
                        else if (File.Exists(path))
                            vm.AddFile(path);
                    }
                }
            }
            else e.DragEffects = DragDropEffects.None;
        }

        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>() is FluentAvaloniaTheme thm)
        {
            thm.RequestedThemeChanged += OnRequestedThemeChanged;

            // Enable Mica on Windows 11
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (IsWindows11 && thm.RequestedTheme != FluentAvaloniaTheme.HighContrastModeString)
                {
                    TransparencyBackgroundFallback = Brushes.Transparent;
                    TransparencyLevelHint = WindowTransparencyLevel.Mica;

                    TryEnableMicaEffect(thm);
                }
            }

            thm.ForceWin32WindowToTheme(this);
        }
    }

    private void OnRequestedThemeChanged(FluentAvaloniaTheme sender, RequestedThemeChangedEventArgs args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: add Windows version to CoreWindow
            if (IsWindows11 && args.NewTheme != FluentAvaloniaTheme.HighContrastModeString)
            {
                TryEnableMicaEffect(sender);
            }
            else if (args.NewTheme == FluentAvaloniaTheme.HighContrastModeString)
            {
                // Clear the local value here, and let the normal styles take over for HighContrast theme
                SetValue(BackgroundProperty, AvaloniaProperty.UnsetValue);
            }
        }
    }

    private void TryEnableMicaEffect(FluentAvaloniaTheme thm)
    {
#pragma warning disable CS8605 // Unboxing possibly null value.
        if (thm.RequestedTheme == FluentAvaloniaTheme.DarkModeString)
        {
            var color = this.TryFindResource("SolidBackgroundFillColorBase", out var value) ? (Color2)(Color)value : new Color2(32, 32, 32);

            color = color.LightenPercent(-0.8f);

            Background = new ImmutableSolidColorBrush(color, 0.78);
        }
        else if (thm.RequestedTheme == FluentAvaloniaTheme.LightModeString)
        {
            // Similar effect here
            var color = this.TryFindResource("SolidBackgroundFillColorBase", out var value) ? (Color2)(Color)value : new Color2(243, 243, 243);

            color = color.LightenPercent(0.5f);

            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
#pragma warning restore CS8605
    }
}
