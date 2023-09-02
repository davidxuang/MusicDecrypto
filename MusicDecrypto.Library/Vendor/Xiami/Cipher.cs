using System;
using System.Numerics;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Xiami;

internal sealed class Cipher : IDecryptor
{
    private readonly byte _key;

    public Cipher(byte key)
    {
        _key = key;
    }

    public long Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        var k = new Vector<byte>(_key);
        for (int i = 0; i < data.Length; i += step)
        {
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            (~(v - k)).CopyTo(window);
        }
        return offset + data.Length;
    }
}