using System;
using System.IO;
using System.Security.Cryptography;

namespace MusicDecrypto
{
    internal abstract class Decrypto
    {
        public static bool AvoidOverwrite { get; set; } = false;
        public static long SaveCount { get; private set; } = 0;
        public string SrcPath { get; protected set; }
        protected BinaryReader SrcFile { get; set; } = null;
        protected MemoryStream Buffer { get; set; } = new MemoryStream();
        protected MemoryStream CoverBuffer { get; set; } = new MemoryStream();
        protected string CoverMime { get; set; }
        protected string MusicMime { get; set; }
        public Metadata StdMetadata { get; protected set; }

        protected Decrypto(string path, string mime = null)
        {
            SrcPath = path;
            SrcFile = new BinaryReader(new FileStream(SrcPath, FileMode.Open));
            MusicMime = mime;
        }

        ~Decrypto()
        {
            SrcFile.Dispose();
            Buffer.Dispose();
            CoverBuffer.Dispose();
        }

        protected abstract void Load();

        protected virtual void FixMetadata() { }

        protected void Save()
        {
            string extension = MusicMime switch
            {
                "audio/flac" => "flac",
                "audio/mpeg" => "mp3",
                _ => throw new FileLoadException($"Failed to recognize music in {SrcPath}."),
            };
            string path = $"{Path.Combine(Path.GetDirectoryName(SrcPath), Path.GetFileNameWithoutExtension(SrcPath))}.{extension}";

            if (File.Exists(path) && AvoidOverwrite)
            {
                Console.WriteLine($"[INFO] Skipping");
                return;
            }

            using FileStream file = new FileStream(path, FileMode.OpenOrCreate);
            Buffer.WriteTo(file);
            SaveCount += 1;
            Console.WriteLine($"[INFO] File was decrypted successfully at {path}.");
        }

        protected byte[] ReadFixedChunk(ref int size)
        {
            byte[] chunk = new byte[size];
            size = SrcFile.Read(chunk, 0, size);
            return chunk;
        }

        protected byte[] ReadIndexedChunk(byte? obfuscator)
        {
            int chunkSize = SrcFile.ReadInt32();

            if (chunkSize > 0)
            {
                byte[] chunk = new byte[chunkSize];
                SrcFile.Read(chunk, 0, chunkSize);
                if (obfuscator != null)
                {
                    for (int i = 0; i < chunkSize; i += 1)
                        chunk[i] ^= obfuscator.Value;
                }
                return chunk;
            }
            else
            {
                throw new NullFileChunkException("Failed to load file chunk.");
            }
        }
    }

    internal class NullFileChunkException : IOException
    {
        internal NullFileChunkException(string message)
            : base(message) { }
    }

    internal class AesCrypto
    {
        public static byte[] EcbDecrypt(byte[] cipher, byte[] key)
        {
            using RijndaelManaged rijndael = new RijndaelManaged
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            using ICryptoTransform cryptoTransform = rijndael.CreateDecryptor();
            byte[] plain = cryptoTransform.TransformFinalBlock(cipher, 0, cipher.Length);
            return plain;
        }
    }
}
