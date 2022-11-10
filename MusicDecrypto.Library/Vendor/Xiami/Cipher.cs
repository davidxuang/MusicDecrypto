using MusicDecrypto.Library.Numerics;
using System.Numerics;
using System;

namespace MusicDecrypto.Library.Vendor.Xiami;

internal sealed class Cipher : IDecryptor
{
    private readonly byte _key;

    public Cipher(byte key)
    {
        _key = key;
    }

    public void Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        var k = new Vector<byte>(_key);
        for (int i = 0; i < data.Length; i += step)
        {
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            (~(v - k)).CopyTo(window);
        }
    }
}