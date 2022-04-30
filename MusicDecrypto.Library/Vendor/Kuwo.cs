using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class Kuwo : DecryptoBase
    {
        private static readonly byte[] _magic = { 0x79, 0x65, 0x65, 0x6c, 0x69, 0x6f, 0x6e, 0x2d, 0x6b, 0x75, 0x77, 0x6f, 0x2d, 0x74, 0x6d, 0x65 };
        private static readonly byte[] _root;
        private static readonly int _paddedMaskSize = Simd.GetPaddedLength(0x20);

        static Kuwo()
        {
            _root = Simd.Align((ReadOnlySpan<byte>)Encoding.ASCII.GetBytes("MoOtOiTvINGwd2E6n0E1i7L5t2IoOoNk").AsSpan());
        }

        public Kuwo(MarshalMemoryStream buffer, string name) : base(buffer, name) { }

        protected override void Process()
        {
            if (_buffer.Length < 1024)
                throw new InvalidDataException("File is too small.");
            if (!Reader.ReadBytes(16).SequenceEqual(_magic))
                throw new InvalidDataException("File header is unexpected.");

            _ = _buffer.Seek(8, SeekOrigin.Current);

            var mask = (stackalloc byte[_paddedMaskSize]);
            PadKey(mask, Encoding.ASCII.GetBytes(Reader.ReadUInt32().ToString()));
            Simd.Align(mask, 0x20);
            int step = Simd.LaneCount;
            for (int i = 0; i < _paddedMaskSize; i += step)
            {
                var window = mask[i..(i + step)];
                var k = new Vector<byte>(window);
                var r = new Vector<byte>(_root[i..(i + step)]);
                (k ^ r).CopyTo(window);
            }

            _buffer.Origin = 0x400;
            var data = _buffer.AsSimdPaddedSpan();
            int i_m;
            for (int i = 0; i < data.Length; i += step)
            {
                i_m = i % 0x20;
                var window = data[i..(i + step)];
                var v = new Vector<byte>(window);
                var m = new Vector<byte>(mask[i_m..(i_m + step)]);
                (v ^ m).CopyTo(window);
            }
        }

        private static void PadKey(Span<byte> buffer, byte[] key)
        {
            if (key.Length >= 32) key.AsSpan(0, 32).CopyTo(buffer);
            key.CopyTo(buffer);
            var copied = key.Length;
            while (buffer.Length > copied)
            {
                buffer[..Math.Min(key.Length, buffer.Length - copied)].CopyTo(buffer[copied..]);
                copied += key.Length;
            }
        }

        private static byte[] PadKey(byte[] key)
        {
            if (key.Length >= 32) return key;
            byte[] pad = new byte[32];
            for (int i = 0; i < 32; i++)
                pad[i] = key[i % key.Length];
            return pad;
        }
    }
}
