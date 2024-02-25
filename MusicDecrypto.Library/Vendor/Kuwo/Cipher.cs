using System;
using System.Numerics;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kuwo;

internal readonly struct Cipher(ReadOnlySpan<byte> mask) : IDecryptor
{
    internal const int MaskSize = 0x20;
    private readonly NanoByteArray _mask = new(mask, PaddingMode.Circular);

    public long Decrypt(Span<byte> data, long offset)
    {
        int step = SimdHelper.LaneCount;
        int offset_m;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_m = (int)((offset + i) % MaskSize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var m = new Vector<byte>(_mask[offset_m..(offset_m + step)]);
            (v ^ m).CopyTo(window);
        }
        return data.Length;
    }
}