using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MusicDecrypto.Library;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MusicDecrypto.Commandline;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp<DecryptoCommand>();
        return await app.RunAsync(args);
    }
}

internal sealed class DecryptoCommand : AsyncCommand<DecryptoCommand.Settings>
{
    private static readonly string[] _extensive = new[] { ".mp3", ".m4a", ".wav", ".flac" };
    private static readonly HashSet<string> _extensions = DecryptoFactory.KnownExtensions.Where(e => !_extensive.Contains(e)).ToHashSet();
    private static readonly SemaphoreSlim _stdinLock = new(1);

    public sealed class Settings : CommandSettings
    {
#pragma warning disable CS8618
        [Description("Input files/directories")]
        [CommandArgument(0, "<input>")]
        public string[] Inputs { get; init; }
#pragma warning restore CS8618

        [Description("Overwrite existing files")]
        [CommandOption("-f|--force-overwrite")]
        public bool ForceOverwrite { get; init; }
        [Description("Search files recursively")]
        [CommandOption("-r|--recursive")]
        public bool Recursive { get; init; }
        [Description("Extend range of extensions to be detected")]
        [CommandOption("-x|--extensive")]
        public bool Extensive { get; init; }
        [Description("Output directory")]
        [CommandOption("-o|--output")]
        public string? Output { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            if (settings.Output != null && !Directory.Exists(settings.Output))
            {
                Log($"Ignoring output directory which does not exist. ({settings.Output})", LogLevel.Error);
                settings.Output = null;
            }

            var files = new HashSet<string>();
            var searchOption = settings.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            if (settings.Extensive) { _extensions.UnionWith(_extensive); }
            foreach (string item in settings.Inputs)
            {
                if (Directory.Exists(item))
                {
                    files.UnionWith(
                        Directory.GetFiles(item, "*", searchOption)
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
                return -1;
            }

            ulong failed = 0, skipped = 0, saved = 0;
            var logQueue = new ConcurrentQueue<(string, LogLevel)>();

            var jobs = Parallel.ForEachAsync(files, async (f, _) => {
                try
                {
                    using var buffer = new MarshalMemoryStream();
                    await using (var file = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        buffer.SetLengthWithPadding(file.Length);
                        await file.CopyToAsync(buffer);
                    }

                    using var decrypto = DecryptoFactory.Create(
                        buffer,
                        Path.GetFileName(f),
                        m => logQueue.Enqueue(($"{f}|{m}", LogLevel.Warn)),
                        OnRequestMatchAsync);

                    var outName = (await decrypto.DecryptAsync()).NewName;
                    var outPath = Path.Combine(Path.GetDirectoryName(settings.Output ?? f)!, outName);

                    if (!File.Exists(outPath) || settings.ForceOverwrite)
                    {
                        await using var file = new FileStream(
                            outPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None);
                        await buffer.CopyToAsync(file);
                        logQueue.Enqueue(($"{f}|File has been decrypted.", LogLevel.Info));
                        saved++;
                    }
                    else
                    {
                        logQueue.Enqueue(($"{f}|File has been skipped.", LogLevel.Info));
                        skipped++;
                    }
                }
                catch (Exception e)
                {
                    logQueue.Enqueue(($"{f}|Decryption has failed.", LogLevel.Error));
                    AnsiConsole.WriteException(e);
                    failed++;
                }
            });

            do
            {
                await Task.Delay(100);
                if (!logQueue.IsEmpty)
                {
                    await _stdinLock.WaitAsync();
                    try
                    {
                        while (logQueue.TryDequeue(out var item))
                        {
                            Log(item.Item1, item.Item2);
                        }
                    }
                    finally
                    {
                        _stdinLock.Release();
                    }
                }
            } while (!jobs.IsCompleted);
            await jobs;

            Log($"Program complete with {files.Count} files processed. [[ {saved} saved / {skipped} skipped / {failed} failed ]]", LogLevel.Info);
            return 0;
        }
        catch (Exception e)
        {
            Log($"Program failed.", LogLevel.Fatal);
            AnsiConsole.WriteException(e);
            return e.GetHashCode();
        }
    }

    private static async ValueTask<bool> OnRequestMatchAsync(string message, IEnumerable<DecryptoBase.MatchInfo> properties)
    {
        await _stdinLock.WaitAsync();
        try
        {
            var table = new Table();
            table.AddColumns("Source", "Title", "Performers", "Album");
            foreach (var item in properties)
            {
                table.AddRow(item.Key, item.Title, item.Performers, item.Album);
            }
            AnsiConsole.Write(table);

            return AnsiConsole.Confirm(message);
        }
        catch
        {
            return false;
        }
        finally
        {
            _stdinLock.Release();
        }
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
        string color = level switch
        {
            LogLevel.Fatal => "red",
            LogLevel.Error => "orangered1",
            LogLevel.Warn  => "gold1",
            _              => "white",
        };

        AnsiConsole.Markup($"[{color}]{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}|{level.ToString()}|{message}[/]\n");
    }
}
