using System;

namespace MusicDecrypto
{
    enum LogLevel
    {
        Info,
        Warn,
        Error,
        Fatal,
    }

    internal static class Logger
    {
        internal static void Log(string message, LogLevel level)
        {
            Console.ForegroundColor = level switch
            {
                LogLevel.Info  => ConsoleColor.White,
                LogLevel.Warn  => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.DarkRed,
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
            Console.WriteLine(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + '|'
                            + level.ToString().ToUpperInvariant() + '|' + message);
        }

        internal static void Log(string message, string path, LogLevel level)
            => Log($"{message} ({path})", level);
    }
}
