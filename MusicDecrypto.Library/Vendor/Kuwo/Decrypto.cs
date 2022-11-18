using System;
using System.IO;
using System.Linq;
using System.Text;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kuwo
{
    internal sealed class Decrypto : DecryptoBase
    {
        private static readonly byte[] _magic = "yeelion-kuwo"u8.ToArray(); // "yeelion-kuwo-tme"
        private static readonly byte[] _root = "MoOtOiTvINGwd2E6n0E1i7L5t2IoOoNk"u8.ToArray();
        private static readonly int _paddedMaskSize = SimdHelper.GetPaddedLength(0x20);

        private readonly IDecryptor _cipher;
        protected override IDecryptor Decryptor => _cipher;

        public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
        {
            if (_buffer.Length < 1024)
                throw new InvalidDataException("File is too small.");
            if (!Reader.ReadBytes(16)[..12].SequenceEqual(_magic))
                throw new InvalidDataException("File header is unexpected.");

            _ = _buffer.Seek(8, SeekOrigin.Current);

            var mask = (stackalloc byte[0x20]);
            var seed = Encoding.ASCII.GetBytes(Reader.ReadUInt32().ToString());
            SimdHelper.PadCircularly(seed.AsSpan(0, Math.Min(0x20, seed.Length)), mask);

            for (int i = 0; i < 0x20; i++)
            {
                mask[i] ^= _root[i];
            }

            _cipher = new Cipher(mask);
            _buffer.Origin = 0x400;
        }
    }
}
