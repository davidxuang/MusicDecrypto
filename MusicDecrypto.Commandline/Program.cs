using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mono.Options;
using MusicDecrypto.Library;

namespace MusicDecrypto.Commandline;

public static class Program
{
    private static ConsoleColor _pushColor;
    private static readonly string[] _extensive = new[] { ".mp3", ".m4a", ".wav", ".flac" };
    private static readonly HashSet<string> _extensions = DecryptoFactory.KnownExtensions.Keys.Where(e => !_extensive.Contains(e)).ToHashSet();

    public static void Main(string[] args)
    {
        _pushColor = Console.ForegroundColor;
        AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

        bool help = args.Length == 0;

        List<string> input;

        SearchOption search = SearchOption.TopDirectoryOnly;
        bool overwrite = false;
        string? outDir = default;
        var options = new OptionSet
        {
            { "f|force-overwrite", "Overwrite existing files.", f => overwrite = f != null },
            { "r|recursive", "Search files recursively.", r => { if (r != null) search = SearchOption.AllDirectories; } },
            { "x|extensive", "Extend range of extensions to be detected.", x => { if (x != null) _extensions.UnionWith(_extensive); } },
            { "o|output=", "Output directory", o => { if (o != null) outDir = o; } },
            { "h|help", "Show help.", h => help = h != null },
        };

        try
        {
            input = options.Parse(args);

            if (help)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(@"
Usage:
  musicdecrypto [options] [<input>...]

Arguments:
  <input>    Input files/directories.

Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (outDir != null && !Directory.Exists(outDir))
            {
                outDir = null;
                Log($"Ignoring output directory which does not exist. ({outDir})", LogLevel.Error);
            }

            // Search for files
            var files = new HashSet<string>();
            foreach (string item in input)
            {
                if (Directory.Exists(item))
                {
                    files.UnionWith(Directory.GetFiles(item, "*", search)
                                             .Where(path => _extensions.Contains(Path.GetExtension(path).ToLowerInvariant())));
                }
                else if (File.Exists(item))
                {
                    files.Add(Path.GetFullPath(item));
                }
            }

            if (files.Count == 0)
            {
                Log("Found no supported file from specified path(s).", LogLevel.Error);
                return;
            }

            // Decrypt and dump
            int succeeded = 0, saved = 0;

            _ = Parallel.ForEach(files, f =>
            {
                try
                {
                    using var buffer = new MarshalMemoryStream();
                    using (var file = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        buffer.SetLengthWithPadding(file.Length);
                        file.CopyTo(buffer);
                    }

                    using var decrypto = DecryptoFactory.Create(
                        buffer,
                        Path.GetFileName(f),
                        m => Log($"{f} - {m}", LogLevel.Warn));

                    var outName = decrypto.Decrypt().NewName;
                    var outPath = Path.Combine(Path.GetDirectoryName(outDir ?? f)!, outName);

                    succeeded++;

                    if (!File.Exists(outPath) || overwrite)
                    {
                        using var file = new FileStream(
                            outPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);
                        buffer.CopyTo(file);
                        Log($"{f} decrypted.", LogLevel.Info);
                        saved++;
                    }
                    else Log($"{f} skipped.", LogLevel.Info);
                }
                catch (Exception e)
                {
                    Log($"{f}\n{e}", LogLevel.Fatal);
                }
            });

            Log($"Program complete. ({files.Count} total, {succeeded} succeeded, {saved} saved)", LogLevel.Info);
        }
        catch (OptionException e)
        {
            Log(e.ToString(), LogLevel.Fatal);
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Console.ForegroundColor = _pushColor;
    }

    private enum LogLevel
    {
        Info,
        Warn,
        Error,
        Fatal,
    }

    private static void Log(string message, LogLevel level)
    {
        Console.ForegroundColor = level switch
        {
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.DarkRed,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
        Console.WriteLine(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + '|'
                        + level.ToString().ToUpperInvariant() + '|' + message);
    }
}
