using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            string[] inputPaths = null;

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts =>
                {
                    if (opts.OutputDir != null)
                    {
                        if (Directory.Exists(opts.OutputDir)) Decrypto.OutputDir = opts.OutputDir;
                        else Console.WriteLine($"[WARN] Specified output directory {opts.OutputDir} does not exist.");
                    }
                    Decrypto.AvoidOverwrite = opts.AvoidOverwrite;
                    inputPaths = opts.InputPaths.ToArray();
                })
                .WithNotParsed<Options>(errs => { });

            if (inputPaths != null)
            {
                List<string> foundPaths = new List<string>();

                foreach (string path in inputPaths)
                {
                    try
                    {
                        if (Directory.Exists(path))
                        {
                            foundPaths.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                                    .Where(file =>
                                                        file.ToLower().EndsWith(".ncm") ||
                                                        file.ToLower().EndsWith(".qmc0") ||
                                                        file.ToLower().EndsWith(".qmc3") ||
                                                        file.ToLower().EndsWith(".qmcflac"))
                                                    );
                        }
                        else if (File.Exists(path) && (
                            path.ToLower().EndsWith(".ncm") ||
                            path.ToLower().EndsWith(".qmc0") ||
                            path.ToLower().EndsWith(".qmc3") ||
                            path.ToLower().EndsWith(".qmcflac")))
                        {
                            foundPaths.Add(path);
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                string[] trimmedPaths = foundPaths.Where((x, i) => foundPaths.FindIndex(y => y == x) == i).ToArray();

                if (trimmedPaths.Length > 0)
                {
                    _ = Parallel.ForEach(trimmedPaths, file =>
                    {
                        try
                        {
                            Decrypto decrypto = null;
                            switch (Path.GetExtension(file))
                            {
                                case ".ncm":
                                    decrypto = new NetEaseDecrypto(file);
                                    break;
                                case ".qmc0":
                                case ".qmc3":
                                    decrypto = new TencentDecrypto(file, "audio/mpeg");
                                    break;
                                case ".qmcflac":
                                    decrypto = new TencentDecrypto(file, "audio/flac");
                                    break;
                                default:
                                    Console.WriteLine($"[WARN] Cannot recognize {file}");
                                    break;
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    });

                    Console.WriteLine($"Program finished with {trimmedPaths.Length} files requested and {Decrypto.SuccessCount} files saved successfully.");
                    return;
                }

                Console.WriteLine("[WARN] Found no valid file from specific path(s).");
            }
        }

        internal class Options
        {
            [Option('a', "avoid-overwrite", Required = false, HelpText = "Do not overwrite existing files.")]
            public bool AvoidOverwrite { get; set; } = false;

            [Option('o', "output", Required = false, HelpText = "Specify output directory for all files.")]
            public string OutputDir { get; set; }

            [Value(0, Required = true, MetaName = "Paths", HelpText = "Specify the input files and/or directories.")]
            public IEnumerable<string> InputPaths { get; set; }
        }
    }
}
