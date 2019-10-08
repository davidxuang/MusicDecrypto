using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MusicDecrypto
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                List<string> paths = new List<string>();

                if (Array.Exists(args, element => element.Equals("-a") || element.Equals("--avoid-overwrite")))
                {
                    Decrypto.AvoidOverwrite = true;
                }
                foreach (string arg in args)
                {
                    try
                    {
                        if (Directory.Exists(arg))
                        {
                            paths.AddRange(Directory.GetFiles(arg, "*.ncm", SearchOption.AllDirectories));
                            paths.AddRange(Directory.GetFiles(arg, "*.qmc0", SearchOption.AllDirectories));
                            paths.AddRange(Directory.GetFiles(arg, "*.qmc3", SearchOption.AllDirectories));
                            paths.AddRange(Directory.GetFiles(arg, "*.qmcflac", SearchOption.AllDirectories));
                        }
                        else if (File.Exists(arg))
                        {
                            paths.Add(arg);
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                if (paths.Count > 0)
                {
                    _ = Parallel.ForEach(paths, path =>
                    {
                        try
                        {
                            Decrypto decrypto = null;
                            switch (Path.GetExtension(path))
                            {
                                case ".ncm":
                                    decrypto = new NetEaseDecrypto(path);
                                    break;
                                case ".qmc0":
                                case ".qmc3":
                                    decrypto = new TencentDecrypto(path, "audio/mpeg");
                                    break;
                                case ".qmcflac":
                                    decrypto = new TencentDecrypto(path, "audio/flac");
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    });

                    Console.WriteLine($"Program finished with {paths.Count} files requested and {Decrypto.SaveCount} files saved successfully.");
                    return;
                }
            }

            Console.WriteLine("Usage: MusicDecrypto [-a|--avoid-overwrite] path...");
        }
    }
}
