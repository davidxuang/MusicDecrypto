using System;
using System.IO;
using System.Security.Cryptography;

namespace MusicDecrypto
{
    internal abstract class Decrypto : IDisposable
    {
        public static bool SkipDuplicate { get; set; } = false;
        public static string OutputDir { get; set; } = null;
        public static ulong SuccessCount { get; private set; } = 0;

        public bool Dumped { get; private set; } = false;
        public string InPath { get; private set; } = null;
        public string OutName { get; protected set; } = null;
        protected string CoverMime { get; set; } = null;
        protected string MusicMime { get; set; } = null;
        protected MemoryStream InBuffer { get; set; } = new MemoryStream();
        protected MemoryStream OutBuffer { get; set; } = new MemoryStream();
        protected MemoryStream CoverBuffer { get; set; } = new MemoryStream();
        public Metadata StdMetadata { get; protected set; }

        protected Decrypto(string path, string mime = null)
        {
            InPath = path;
            using FileStream file = new FileStream(InPath, FileMode.Open);
            file.CopyTo(InBuffer);
            ResetInBuffer();
            MusicMime = mime;
        }

        public virtual void Dispose()
        {
            InBuffer.Dispose();
            OutBuffer.Dispose();
            CoverBuffer.Dispose();
        }

        public void Dump()
        {
            if (!Dumped)
            {
                Check();
                Decrypt();
                Metadata();
                Save();
                Dumped = true;
            }
        }

        protected virtual void Check() { }
        protected abstract void Decrypt();
        protected abstract void Metadata();

        protected void Save()
        {
            string extension;
            try
            {
                extension = MediaType.MimeToExt(MusicMime);
            }
            catch (InvalidDataException)
            {
                throw new FileLoadException($"Failed to recognize music in {InPath}.");
            }

            string path;
            if (OutName == null) OutName = Path.GetFileNameWithoutExtension(InPath);
            path = ((OutputDir == null) ? Path.Combine(Path.GetDirectoryName(InPath), OutName) : Path.Combine(OutputDir, OutName)) + $".{extension}";

            if (File.Exists(path) && SkipDuplicate)
            {
                Console.WriteLine($"[INFO] Skipping {path}");
                return;
            }

            using FileStream file = new FileStream(path, FileMode.Create);
            OutBuffer.WriteTo(file);
            SuccessCount++;
            Console.WriteLine($"[INFO] File was decrypted successfully at {path}.");
        }

        protected byte[] ReadFixedChunk(ref int size)
        {
            byte[] chunk = new byte[size];
            size = InBuffer.Read(chunk, 0, size);
            return chunk;
        }

        protected void ResetInBuffer()
        {
            InBuffer.Position = 0;
        }
        protected void ResetOutBuffer()
        {
            OutBuffer.Position = 0;
        }

        public static byte[] AesEcbDecrypt(byte[] cipher, byte[] key)
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

    internal class NullFileChunkException : IOException
    {
        internal NullFileChunkException(string message)
            : base(message) { }
    }
}
