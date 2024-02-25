using System;
using System.Numerics;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Xiami;

internal sealed class Cipher(byte key) : IDecryptor
{
    public long Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        var k = new Vector<byte>(key);
        for (int i = 0; i < data.Length; i += step)
        {
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            (~(v - k)).CopyTo(window);
        }
        return data.Length;
    }
}