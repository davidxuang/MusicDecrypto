using System;
using System.Numerics;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kuwo;

internal sealed class Cipher(ReadOnlySpan<byte> mask) : IDecryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _mask = SimdHelper.PadCircularly(mask);
    private const int _maskSize = 0x20;

    public void Dispose()
    {
        _mask.Dispose();
    }

    public long Decrypt(Span<byte> data, long offset)
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
        return offset + data.Length;
    }
}