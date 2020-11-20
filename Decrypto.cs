using System;
using System.IO;
using System.Security.Cryptography;

namespace MusicDecrypto
{
    public abstract class Decrypto : IDisposable
    {
        protected readonly FileInfo _input;
        protected string _outName;
        protected bool _dumped;
        protected MusicTypes? _musicType;
        protected ExtendedMemoryStream _buffer = new ExtendedMemoryStream();
        protected BinaryReader _reader;
        protected ImageTypes? _coverType;
        protected byte[] _coverBuffer;

        public static bool ForceOverwrite { get; set; }
        public static DirectoryInfo Output { get; set; }
        public static ulong DumpCount { get; private set; }

        protected Decrypto(FileInfo file, MusicTypes? type = null)
        {
            _input = file;
            using FileStream stream = file.OpenRead();
            stream.CopyTo(_buffer);
            _buffer.ResetPosition();
            _musicType = type;
            _reader = new BinaryReader(_buffer);
        }

        public void Dispose()
        {
            _reader.Dispose();
            _buffer.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dump()
        {
            if (!_dumped)
            {
                PreDecrypt();
                Decrypt();
                PostDecrypt();
                Save();
                _dumped = true;
            }
        }

        protected virtual void PreDecrypt() { }
        protected abstract void Decrypt();
        protected virtual void PostDecrypt() { _buffer.Name = "buffer." + _musicType?.GetExtension(); }

        protected void Save()
        {
            string extension;
            extension = _musicType?.GetExtension();
            if (extension == null)
                throw new DecryptoException("Unable to determine output extension.", _input.FullName);

            string path;
            if (_outName == null) _outName = Path.GetFileNameWithoutExtension(_input.FullName);
            _outName += extension;
            if (_outName == _input.Name) _outName += extension;
            path = (Output == null) ? Path.Combine(_input.DirectoryName, _outName)
                                    : Path.Combine(Output.FullName, _outName);

            if (File.Exists(path) && !ForceOverwrite)
            {
                Logger.Log("Skipping existing file.", path, LogLevel.Warn);
                return;
            }

            using var file = new FileStream(path, FileMode.Create);
            _buffer.WriteTo(file);
            DumpCount++;
            Logger.Log("File was successfully decrypted.", path, LogLevel.Info);
        }
    }

    public static class DecryptExtensions
    {
        public static byte[] AesEcbDecrypt(this byte[] cipher, byte[] key)
        {
            using var rijndael = new RijndaelManaged
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            using ICryptoTransform cryptoTransform = rijndael.CreateDecryptor();
            return cryptoTransform.TransformFinalBlock(cipher, 0, cipher.Length);
        }
    }

    public class DecryptoException : IOException
    {
        public DecryptoException(string message, string path)
            : base($"{message} ({path})") { }

        public DecryptoException(string message, string path, Exception innerException)
            : base($"{message} ({path})", innerException) { }
    }

    public class NullFileChunkException : IOException
    {
        public NullFileChunkException(string message)
            : base(message) { }
    }
}
