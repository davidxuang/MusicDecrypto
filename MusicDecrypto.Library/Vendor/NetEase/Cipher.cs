using System;
using System.Numerics;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.NetEase;

internal sealed class Cipher : IDecryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _mask;
    private const int _maskSize = 0x100;

    public Cipher(ReadOnlySpan<byte> mask)
    {
        _mask = SimdHelper.PadCircularly(mask);
    }

    public void Dispose()
    {
        _mask.Dispose();
    }

    public void Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        int i_m;
        for (int i = 0; i < data.Length; i += step)
        {
            i_m = (int)((offset + i) % _maskSize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var m = new Vector<byte>(_mask.AsSpan(i_m, step));
            (v ^ m).CopyTo(window);
        }
    }
}
