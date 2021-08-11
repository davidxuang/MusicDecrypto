using MusicDecrypto.Library.Common;
using System.IO;
using System.Linq;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class XiamiDecrypto : Decrypto
    {
        private static readonly byte[] _magic = { 0x69, 0x66, 0x6d, 0x74 };
        private static readonly byte[] _separator = { 0x69, 0x66, 0x6d, 0x74 };

        public XiamiDecrypto(FileInfo file, AudioTypes type = AudioTypes.Undefined) : base(file, type) { }

        protected override void PreDecrypt()
        {
            // Check file header
            if (!_reader.ReadBytes(8).Take(4).SequenceEqual(_magic) || !_reader.ReadBytes(4).SequenceEqual(_separator))
            {
                if (_input.Extension.TrimStart('.') == "xm")
                    throw new DecryptoException("File header is unexpected.", _input.FullName);
                else
                    throw new DecryptoException("File seems unencrypted.", _input.FullName);
            }
        }

        protected override void Decrypt()
        {
            _ = _buffer.Seek(4, SeekOrigin.Begin);
            string identifier = _reader.ReadChars(4).ToString();
            if (_musicType == AudioTypes.Undefined) _musicType = identifier switch
            {
                " A4M" => AudioTypes.Mp4,
                "FLAC" => AudioTypes.Flac,
                " MP3" => AudioTypes.Mpeg,
                " WAV" => AudioTypes.Wav,
                _ => throw new DecryptoException("Unable to determine media format.", _input.FullName),
            };

            int offset = _reader.ReadByte() | _reader.ReadByte() << 8 | _reader.ReadByte() << 16;
            byte key = _reader.ReadByte();
            _buffer.Origin = 0x10 + offset;
            _buffer.PerformEach(x => (byte)((x - key) ^ 0xff));
        }

        protected override void PostDecrypt() { }
    }
}
