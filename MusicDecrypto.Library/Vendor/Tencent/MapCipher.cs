using System;

namespace MusicDecrypto.Library.Vendor.Tencent
{
    public class MapCipher : MaskCipherBase
    {
        private readonly byte[] _box;
        private readonly int _size;

        public MapCipher(byte[] key)
        {
            if (key.Length == 0)
                throw new ArgumentException("Key is empty.");

            _box = key;
            _size = key.Length;
        }

        protected override void GetMask(Span<byte> buffer, long indexOffset)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var index = indexOffset + i;
                if (index > 0x7fff) index %= 0x7fff;
                var offset = (index * index + 71214) % _size;
                buffer[i] = Rotate(_box[offset], (byte)(offset & 0x07));
            }
        }

        private static byte Rotate(byte value, byte bits)
        {
            var rotate = (byte)((bits + 4) % 8);
            var left = value << rotate;
            var right = value >> rotate;
            return (byte)(left | right);
        }
    }
}
