using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    public static class Program
    {
        private static ConsoleColor _pushColor;
        private static readonly HashSet<string> _extension
            = new HashSet<string> { ".ncm", ".tm2", ".tm6", ".qmc0", ".qmc3", ".bkcmp3", ".qmcogg", ".qmcflac", ".tkm", ".bkcflac", ".mflac", ".xm" };

        public static void Main(string[] args)
        {
            _pushColor = Console.ForegroundColor;
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            List<string> input;
            SearchOption search = SearchOption.TopDirectoryOnly;
            bool help = args.Length == 0;
            var options = new OptionSet
            {
                { "f|force-overwrite", "Overwrite existing files.", f => Decrypto.ForceOverwrite = f != null },
                { "n|renew-name", "Renew Hash-like names basing on metadata.", n => TencentDecrypto.RenewName = n != null },
                { "r|recursive", "Search files recursively.", r => { if (r != null) search = SearchOption.AllDirectories; } },
                { "x|extensive", "Extend range of extensions to be detected.", x => { if (x != null) _extension.UnionWith(new []{ ".mp3", ".m4a", ".wav", ".flac" }); } },
                { "o|output=", "Output directory", o => { if (o != null) Decrypto.Output = new DirectoryInfo(o); } },
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
  MusicDecrypto [options] [<input>...]

Arguments:
  <input>    Input files/directories.

Options:");
                    options.WriteOptionDescriptions(Console.Out);
                    return;
                }

                if (Decrypto.Output?.Exists == false)
                {
                    Logger.Log("Ignore output directory which does not exist.", Decrypto.Output.FullName, LogLevel.Error);
                }

                // Search for files
                var files = new HashSet<FileInfo>(new FileInfoComparer());
                foreach (string item in input)
                {
                    if (Directory.Exists(item))
                    {
                        files.UnionWith(Directory.GetFiles(item, "*", search)
                                                 .Where(path => _extension.Contains(Path.GetExtension(path).ToLowerInvariant()))
                                                 .Select(path => new FileInfo(path)));
                    }
                    else if (File.Exists(item))
                    {
                        files.Add(new FileInfo(item));
                    }
                }

                if (files.Count == 0)
                {
                    Logger.Log("Found no supported file from specified path(s).", LogLevel.Error);
                    return;
                }

                // Decrypt and dump
                _ = Parallel.ForEach(files, file =>
                {
                    try
                    {
                        using Decrypto decrypto = file.Extension switch
                        {
                            ".ncm"    => new NetEaseDecrypto(file),
                            ".tm2" or ".tm6"
                                      => new TencentSimpleDecrypto(file, MusicTypes.XM4a),
                            ".qmc0" or ".qmc3" or ".bkcmp3"
                                      => new TencentStaticDecrypto(file, MusicTypes.Mpeg),
                            ".qmcogg" => new TencentStaticDecrypto(file, MusicTypes.Ogg),
                            ".tkm"    => new TencentStaticDecrypto(file, MusicTypes.XM4a),
                            ".qmcflac" or ".bkcflac"
                                      => new TencentStaticDecrypto(file, MusicTypes.Flac),
                            ".mflac"  => new TencentDynamicDecrypto(file, MusicTypes.Flac),
                            ".xm"     => new XiamiDecrypto(file, null),
                            ".mp3"    => new XiamiDecrypto(file, MusicTypes.Mpeg),
                            ".m4a"    => new XiamiDecrypto(file, MusicTypes.XM4a),
                            ".wav"    => new XiamiDecrypto(file, MusicTypes.XWav),
                            ".flac"   => new XiamiDecrypto(file, MusicTypes.Flac),
                            _ => throw new DecryptoException("File has an unsupported extension.", file.FullName)
                        };

                        decrypto?.Dump();
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.ToString(), LogLevel.Fatal);
                    }
                });

                Logger.Log($"Program finished with {Decrypto.DumpCount}/{files.Count} files decrypted successfully.", LogLevel.Info);
            }
            catch (OptionException e)
            {
                Logger.Log(e.ToString(), LogLevel.Fatal);
            }
        }

        private static void OnProcessExit (object sender, EventArgs e)
        {
            Console.ForegroundColor = _pushColor;
        }
    }

    internal class FileInfoComparer : IEqualityComparer<FileInfo>
    {
        public bool Equals(FileInfo x, FileInfo y) => Equals(x.FullName, y.FullName);
        public int GetHashCode([DisallowNull] FileInfo obj) => obj.FullName.GetHashCode();
    }
}
