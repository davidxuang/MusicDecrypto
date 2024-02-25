using System;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Numerics;

[InlineArray(Size)]
internal struct NanoByteArray
{
    public const int Size = 256;
#pragma warning disable IDE0044,IDE0051
    private byte _byte;
#pragma warning restore IDE0044,IDE0051

    static NanoByteArray()
    {
        if (SimdHelper.LaneCount * 2 > Size)
        {
            throw new NotSupportedException();
        }
    }

    public NanoByteArray(ReadOnlySpan<byte> data, PaddingMode mode = PaddingMode.Zero)
    {
        SimdHelper.Pad(data, this, mode);
    }
}
