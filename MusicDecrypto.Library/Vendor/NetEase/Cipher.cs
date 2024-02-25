using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.NetEase;

[InlineArray(MaskSize + NanoByteArray.Size)]
internal struct Cipher : IDecryptor
{
    internal const int MaskSize = 0x100;
#pragma warning disable IDE0044,IDE0051
    private byte _mask;
#pragma warning restore IDE0044,IDE0051

    public Cipher(ReadOnlySpan<byte> mask)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(mask.Length, MaskSize, nameof(mask));
        SimdHelper.Pad(mask, this, PaddingMode.Circular);
    }

    public readonly long Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        int offset_m;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_m = (int)((offset + i) % MaskSize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var m = new Vector<byte>(this[offset_m..(offset_m + step)]);
            (v ^ m).CopyTo(window);
        }
        return data.Length;
    }
}
