using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class Xiami : DecryptoBase
    {
        private static readonly byte[] _magic = { 0x69, 0x66, 0x6d, 0x74 };
        private static readonly byte[] _separator = { 0xfe, 0xfe, 0xfe, 0xfe };

        public Xiami(MarshalMemoryStream buffer, string name, AudioTypes type = AudioTypes.Undefined) : base(buffer, name, type) { }

        protected override void Process()
        {
            // Check file header
            if (!Reader.ReadBytes(8).AsSpan(0, 4).SequenceEqual(_magic) || !Reader.ReadBytes(4).SequenceEqual(_separator))
            {
                throw new InvalidDataException(
                    Path.GetExtension(Name).TrimStart('.') == "xm"
                    ? "File header is unexpected."
                    : "File seems unencrypted.");
            }

            _ = _buffer.Seek(4, SeekOrigin.Begin);
            var identifier = Encoding.ASCII.GetString(Reader.ReadBytes(4));
            if (_audioType == AudioTypes.Undefined) _audioType = identifier switch
            {
                " A4M" => AudioTypes.Mp4,
                "FLAC" => AudioTypes.Flac,
                " MP3" => AudioTypes.Mpeg,
                " WAV" => AudioTypes.XWav,
                _ => throw new InvalidDataException("Unable to determine media format."),
            };

            _ = _buffer.Seek(12, SeekOrigin.Begin);
            int offset = Reader.ReadByte() | Reader.ReadByte() << 8 | Reader.ReadByte() << 16;
            byte key = Reader.ReadByte();
            _buffer.Origin = 0x10;

            int step = Simd.LaneCount;
            var k = new Vector<byte>(key);
            var data = _buffer.AsSimdPaddedSpan(offset);
            for (int i = 0; i < data.Length; i += step)
            {
                var window = data[i..(i + step)];
                var v = new Vector<byte>(window);
                (~(v - k)).CopyTo(window);
            }
        }
    }
}
