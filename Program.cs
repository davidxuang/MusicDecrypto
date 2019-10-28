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
            if (args.Length > 0)
            {
                List<string> found = new List<string>();

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
                            found.AddRange(Directory.EnumerateFiles(arg, "*", SearchOption.AllDirectories)
                                                    .Where(file =>
                                                        file.ToLower().EndsWith("ncm") ||
                                                        file.ToLower().EndsWith("qmc0") ||
                                                        file.ToLower().EndsWith("qmc3") ||
                                                        file.ToLower().EndsWith("qmcflac"))
                                                    .ToList());
                        }
                        else if (File.Exists(arg))
                        {
                            found.Add(arg);
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                string[] trimmed = found.Where((x, i) => found.FindIndex(y => y == x) == i).ToArray();

                if (trimmed.Length > 0)
                {
                    _ = Parallel.ForEach(trimmed, path =>
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

                    Console.WriteLine($"Program finished with {trimmed.Length} files requested and {Decrypto.SaveCount} files saved successfully.");
                    return;
                }
            }

            Console.WriteLine("Usage: MusicDecrypto [-a|--avoid-overwrite] path...");
        }
    }
}
