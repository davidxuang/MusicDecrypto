using System;
using Avalonia;

namespace MusicDecrypto.Avalonia
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions()
                {
                    UseWindowsUIComposition = true,
                    EnableMultitouch = true,
                    CompositionBackdropCornerRadius = 8f
                });
    }
}
