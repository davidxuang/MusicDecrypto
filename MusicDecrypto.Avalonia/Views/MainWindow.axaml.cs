using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Windowing;
using MusicDecrypto.Avalonia.ViewModels;

namespace MusicDecrypto.Avalonia.Views;

public partial class MainWindow : AppWindow
{
    public MainWindow()
    {
        DataContext = new MainViewModel();

        InitializeComponent();

        SetupDnd();

#if DEBUG
        this.AttachDevTools();
#endif

        // SplashScreen = new MainAppSplashScreen(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

        Application.Current!.ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private void SetupDnd()
    {
        void DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects &= DragDropEffects.Copy;

            if (!e.Data.Contains(DataFormats.Files))
                e.DragEffects = DragDropEffects.None;
        }

        void Drop(object? sender, DragEventArgs e)
        {
            e.DragEffects &= DragDropEffects.Copy;

            if (e.Data.Contains(DataFormats.Files) && DataContext is MainViewModel vm)
            {
                foreach (var item in e.Data.GetFiles()!)
                {
                    if (item is IStorageFolder folder)
                    {
                        foreach (var child in folder.GetItemsAsync().ToBlockingEnumerable())
                        {
                            if (child is IStorageFile file)
                            {
                                vm.AddFile(file);
                            }
                        }
                    }
                    else if (item is IStorageFile file)
                    {
                        vm.AddFile(file);
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

        var theme = ActualThemeVariant;
        if (IsWindows11 && theme != FluentAvaloniaTheme.HighContrastTheme)
        {
            TryEnableMicaEffect();
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (IsWindows11)
        {
            if (ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
            {
                TryEnableMicaEffect();
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(TransparencyBackgroundFallbackProperty);
            }
        }
    }

    private void TryEnableMicaEffect()
    {
        return; // TODO
    }
}
