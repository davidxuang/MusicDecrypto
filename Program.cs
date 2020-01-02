using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            try
            {
                // Setup NLog
                var logConfig = new NLog.Config.LoggingConfiguration();
                var logFile = new NLog.Targets.FileTarget("logfile") { FileName = "debug.log" };
                var logConsole = new NLog.Targets.ColoredConsoleTarget("logconsole");
                logConfig.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
                logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logFile);
                NLog.LogManager.Configuration = logConfig;

                // Parse options
                string[] inputPaths = null;
                CommandLine.Parser.Default.ParseArguments<Options>(args)
                    .WithParsed<Options>(opts =>
                    {
                        if (opts.OutputDir != null)
                        {
                            if (Directory.Exists(opts.OutputDir)) Decrypto.OutputDir = opts.OutputDir;
                            else Logger.Error("Designated output directory {Path} does not exist.", opts.OutputDir);
                        }
                        Decrypto.SkipDuplicate = opts.SkipDuplicate;
                        TencentDecrypto.ForceRename = opts.ForceRename;
                        inputPaths = opts.InputPaths.ToArray();
                    })
                    .WithNotParsed<Options>(errs => { });

                if (inputPaths != null)
                {
                    // Search for files
                    List<string> foundPaths = new List<string>();
                    foreach (string path in inputPaths)
                    {
                        try
                        {
                            if (Directory.Exists(path))
                            {
                                foundPaths.AddRange(
                                    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                        .Where(file =>
                                            file.ToLower().EndsWith(".ncm") ||
                                            file.ToLower().EndsWith(".mflac") ||
                                            file.ToLower().EndsWith(".qmc0") ||
                                            file.ToLower().EndsWith(".qmc3") ||
                                            file.ToLower().EndsWith(".qmcogg") ||
                                            file.ToLower().EndsWith(".qmcflac"))
                                );
                            }
                            else if (File.Exists(path) && (
                                path.ToLower().EndsWith(".ncm") ||
                                path.ToLower().EndsWith(".mflac") ||
                                path.ToLower().EndsWith(".qmc0") ||
                                path.ToLower().EndsWith(".qmc3") ||
                                path.ToLower().EndsWith(".qmcogg") ||
                                path.ToLower().EndsWith(".qmcflac")))
                            {
                                foundPaths.Add(path);
                            }
                        }
                        catch (IOException e)
                        {
                            Logger.Error(e);
                        }
                    }

                    // Decrypt and dump
                    string[] trimmedPaths = foundPaths.Where((x, i) => foundPaths.FindIndex(y => y == x) == i).ToArray();
                    if (trimmedPaths.Length > 0)
                    {
                        _ = Parallel.ForEach(trimmedPaths, file =>
                        {
                            Decrypto decrypto = null;

                            try
                            {
                                switch (Path.GetExtension(file))
                                {
                                    case ".ncm":
                                        decrypto = new NetEaseDecrypto(file);
                                        break;
                                    case ".qmc0":
                                    case ".qmc3":
                                        decrypto = new TencentFixedDecrypto(file, "audio/mpeg");
                                        break;
                                    case ".qmcogg":
                                        decrypto = new TencentFixedDecrypto(file, "audio/ogg");
                                        break;
                                    case ".qmcflac":
                                        decrypto = new TencentFixedDecrypto(file, "audio/flac");
                                        break;
                                    case ".mflac":
                                        decrypto = new TencentDynamicDecrypto(file, "audio/flac");
                                        break;
                                    default:
                                        Logger.Error("Cannot recognize {Path}", file);
                                        break;
                                }

                                if (decrypto != null) decrypto.Dump();
                            }
                            catch (IOException e)
                            {
                                Logger.Error(e);
                            }
                            finally
                            {
                                if (decrypto != null) decrypto.Dispose();
                            }
                        });

                        Logger.Info("Program finished with {Requested} files requested and {Succeeded} files saved successfully.", trimmedPaths.Length, Decrypto.SuccessCount);
                        return;
                    }

                    Logger.Error("Found no valid file from specified path(s).");
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
            }
        }

        internal class Options
        {
            [Option('d', "skip-duplicate", Required = false, HelpText = "Do not overwrite existing files.")]
            public bool SkipDuplicate { get; set; } = false;

            [Option('n', "force-rename", Required = false, HelpText = "Try to fix Tencent file name basing on metadata.")]
            public bool ForceRename { get; set; } = false;

            [Option('o', "output", Required = false, HelpText = "Specify output directory for all files.")]
            public string OutputDir { get; set; }

            [Value(0, Required = true, MetaName = "Path", HelpText = "Specify the input files and/or directories.")]
            public IEnumerable<string> InputPaths { get; set; }
        }
    }
}
