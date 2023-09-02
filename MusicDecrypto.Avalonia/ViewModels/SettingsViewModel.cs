using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using MusicDecrypto.Avalonia.Helpers;

namespace MusicDecrypto.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly FluentAvaloniaTheme _faTheme;
    public SettingsViewModel()
    {
        _faTheme = (FluentAvaloniaTheme)Application.Current!.Styles[0];
    }

    private string? _currentAppTheme = _systemModeString;
    public string? CurrentAppTheme
    {
        get => _currentAppTheme;
        set
        {
            if (RaiseAndSetIfChanged(ref _currentAppTheme, value))
            {
                if (TryGetThemeVariant(value, out var theme))
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    _faTheme.PreferSystemTheme = false;
                }
                else
                {
                    _faTheme.PreferSystemTheme = true;
                }
            }
        }
    }

    private static bool TryGetThemeVariant(string? value, [NotNullWhen(true)] out ThemeVariant? theme)
    {
        theme = value switch
        {
            FluentAvaloniaTheme.LightModeString => ThemeVariant.Light,
            FluentAvaloniaTheme.DarkModeString  => ThemeVariant.Dark,
            _                                   => null,
        };
        return theme is not null;
    }

    private static string _systemModeString = "System";
    public static string[] AppThemes => new[]
    {
        _systemModeString,
        FluentAvaloniaTheme.LightModeString,
        FluentAvaloniaTheme.DarkModeString,
        // FluentAvaloniaTheme.HighContrastModeString
    };

    public static string Version => typeof(Program).Assembly.GetName().Version!.ToString();

    public static void OpenAvaloniaLink() => UrlHelper.OpenLink("https://avaloniaui.net/");
    public static void OpenFluentAvaloniaLink() => UrlHelper.OpenLink("https://github.com/amwx/FluentAvalonia");

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
