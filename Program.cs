using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    public static class Program
    {
        private static readonly HashSet<string> SupportedExtensions
            = new HashSet<string> { ".ncm", ".mflac", ".qmc0", ".qmc3", ".qmcogg", ".qmcflac", ".tkm", ".tm2", ".tm6", ".bkcmp3", ".bkcflac" };

        public static int Main(string[] args)
        {
            var command = new RootCommand
            {
                new Argument<FileSystemInfo[]>("input", "Input files/directories."),
                new Option<bool>(new[] { "-f", "--force-overwrite"}, "Overwrite existing files."),
                new Option<bool>(new[] { "-n", "--renew-name" }, "Renew Hash-like names basing on metadata."),
                new Option<bool>(new[] { "-r", "--recursive" }, "Search files recursively"),
                new Option<DirectoryInfo>(new[] { "-o", "--output" }, "Output directory."),
            };

            command.Handler = CommandHandler.Create<FileSystemInfo[], bool, bool, bool, DirectoryInfo>((input, forceOverwrite, renewName, recursive, output) =>
            {
                if (input == null) return;
                Decrypto.ForceOverwrite = forceOverwrite;
                TencentDecrypto.RenewName = renewName;
                if (output != null)
                {
                    if (output.Exists)
                        Decrypto.Output = output;
                    else
                        Logger.Log("Ignore output directory which does not exist.", output.FullName, LogLevel.Error);
                }

                // Search for files
                var files = new HashSet<FileInfo>(new FileInfoComparer());
                foreach (FileSystemInfo item in input)
                {
                    if (item?.Exists != true) continue;

                    if (item is DirectoryInfo)
                    {
                        files.UnionWith((item as DirectoryInfo)
                            .GetFiles("*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                            .Where(file => SupportedExtensions.Contains(
                                file.Extension.ToLowerInvariant())));
                    }
                    else
                    {
                        files.Add(item as FileInfo);
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
            });

            if (args.Length == 0)
                return command.Invoke("-h");

            return command.Invoke(args);
        }
    }

    internal class FileInfoComparer : IEqualityComparer<FileInfo>
    {
        public bool Equals(FileInfo x, FileInfo y) => Equals(x.FullName, y.FullName);
        public int GetHashCode([DisallowNull] FileInfo obj) => obj.FullName.GetHashCode();
    }
}
