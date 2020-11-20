using System;
using System.IO;
using System.Linq;

namespace MusicDecrypto
{
    public sealed class XiamiDecrypto : Decrypto
    {
        public XiamiDecrypto(FileInfo file, MusicTypes? type = null) : base(file, type) { }

        protected override void PreDecrypt()
        {
            // Check file header
            uint magic = _reader.ReadUInt32();
            _ = _buffer.Seek(4, SeekOrigin.Current);
            uint separator = _reader.ReadUInt32();
            if (magic != 0x746d6669 || separator != 0xfefefefe)
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
            if (_musicType == null) _musicType = identifier switch
            {
                " A4M" => MusicTypes.XM4a,
                "FLAC" => MusicTypes.Flac,
                " MP3" => MusicTypes.Mpeg,
                " WAV" => MusicTypes.XWav,
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
