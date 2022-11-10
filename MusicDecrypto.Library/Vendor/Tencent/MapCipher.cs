using System;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed class MapCipher : MaskCipherBase
{
    private readonly byte[] _box;
    private readonly int _boxSize;

    public MapCipher(byte[] key)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key should not be empty.", nameof(key));

        _box = key;
        _boxSize = key.Length;
    }

    protected override void GetMask(Span<byte> buffer, long offset)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            var index = offset + i;
            if (index > 0x7fff) index %= 0x7fff;
            var i_b = (index * index + 71214) % _boxSize;
            buffer[i] = Rotate(_box[i_b], (byte)(i_b & 0x07));
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