using System.IO;
using System.Text;

namespace MusicDecrypto
{
    public sealed class KuwoDecrypto : Decrypto
    {
        private static readonly byte[] _root = Encoding.ASCII.GetBytes("MoOtOiTvINGwd2E6n0E1i7L5t2IoOoNk");

        public KuwoDecrypto(FileInfo file) : base(file) { }

        protected override void PreDecrypt()
        {
            if (_buffer.Length < 1024)
                throw new DecryptoException("File is too small.", _input.FullName);
            if (_reader.ReadUInt64() != 0x2d6e6f696c656579 || _reader.ReadUInt64() != 0x656d742d6f77756b)
                throw new DecryptoException("File header is unexpected.", _input.FullName);

            _ = _buffer.Seek(8, SeekOrigin.Current);
        }

        protected override void Decrypt()
        {
            var key = PadKey(Encoding.ASCII.GetBytes(_reader.ReadUInt32().ToString()));
            var mask = new byte[32];
            for (ushort i = 0; i < 32; i++)
                mask[i] = (byte)(_root[i] ^ key[i]);

            _buffer.Origin = 0x400;
            _buffer.PerformEach((x, i) => (byte)(x ^ mask[i]));
            _musicType = _buffer.ToArray().ParseMusicType();
        }

        protected override void PostDecrypt() { }

        private static byte[] PadKey(byte[] key)
        {
            if (key.Length >= 32)
                return key;
            byte[] pad = new byte[32];
            for (ushort i = 0; i < 32; i++)
                pad[i] = key[i % key.Length];
            return pad;
        }
    }
}
