using System;
using Avalonia;
using FluentIcons.Avalonia.Fluent;

namespace MusicDecrypto.Avalonia;

public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseSegoeMetrics()
            .LogToTrace();
}
