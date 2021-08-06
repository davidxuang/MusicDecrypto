namespace MusicDecrypto.Library.Common
{
    public enum LogLevel
    {
        Info,
        Warn,
        Error,
        Fatal,
    }

    public static class Logger
    {
        public delegate void LogHandler(string message, LogLevel level);
        public static event LogHandler LogEvent;

        public static void Log(string message, LogLevel level)
        {
            LogEvent?.Invoke(message, level);
        }
        public static void Log(string message, string path, LogLevel level)
        {
            LogEvent?.Invoke($"{message} ({path})", level);
        }
    }
}
