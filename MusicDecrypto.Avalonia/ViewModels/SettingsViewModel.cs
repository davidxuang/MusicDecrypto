using System;
using Avalonia;
using FluentAvalonia.Styling;
using MusicDecrypto.Avalonia.Helpers;

namespace MusicDecrypto.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        if (AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>() is FluentAvaloniaTheme theme)
        {
            theme.RequestedThemeChanged += OnAppThemeChanged;
            _currentAppTheme = theme.RequestedTheme;
        }
        else throw new NullReferenceException("Can not access FluentAvaloniaTheme.");
    }

    public static string Version => typeof(Program).Assembly.GetName().Version!.ToString();

    public static void OpenAvaloniaLink() => UrlHelper.OpenLink("https://avaloniaui.net/");
    public static void OpenFluentAvaloniaLink() => UrlHelper.OpenLink("https://github.com/amwx/FluentAvalonia");

    private string _currentAppTheme;
    public string CurrentAppTheme
    {
        get => _currentAppTheme;
        set
        {
            if (_currentAppTheme != value)
            {
                if (AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>() is FluentAvaloniaTheme theme)
                {
                    _currentAppTheme = value;
                    theme.RequestedTheme = value;
                }
                PropertyHasChanged(nameof(CurrentAppTheme));
            }
        }
    }

    public static string[] AppThemes => new[]
    {
        FluentAvaloniaTheme.LightModeString,
        FluentAvaloniaTheme.DarkModeString,
        FluentAvaloniaTheme.HighContrastModeString
    };

    private void OnAppThemeChanged(FluentAvaloniaTheme sender, RequestedThemeChangedEventArgs args)
    {
        if (_currentAppTheme != args.NewTheme)
        {
            _currentAppTheme = args.NewTheme;
            PropertyHasChanged(nameof(CurrentAppTheme));
        }
    }

    public record class CreditItem(
        string Name,
        string License,
        string Url);

    private static readonly CreditItem[] _credits = new[]
    {
        new CreditItem(".NET",                  "MIT", "https://dotnet.microsoft.com/"),
        new CreditItem("AvaloniaUI",            "MIT", "https://avaloniaui.net/"),
        new CreditItem("FluentAvalonia",        "MIT", "https://github.com/amwx/FluentAvalonia"),
        new CreditItem("fluentui-system-icons", "MIT", "https://github.com/microsoft/fluentui-system-icons"),
        new CreditItem("NativeMemoryArray",     "MIT", "https://github.com/Cysharp/NativeMemoryArray"),
        new CreditItem("TagLibSharp",      "LGPL-2.1", "https://github.com/mono/taglib-sharp"),
        new CreditItem("ByteSize",              "MIT", "https://github.com/omar/ByteSize"),
        new CreditItem("unlock-music",          "MIT", "https://gitlab.com/ix64/unlock-music"),
    };
    public static CreditItem[] Credits => _credits;
}
