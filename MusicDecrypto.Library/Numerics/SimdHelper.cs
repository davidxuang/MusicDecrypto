using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Cysharp.Collections;

namespace MusicDecrypto.Library.Numerics;

internal static class SimdHelper
{
    public static int LaneCount => Vector<byte>.Count;

    public static int GetPaddedLength(int length) =>
        (length % LaneCount == 0) ? length : length + LaneCount - GetCommonDivisor(length, LaneCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCommonDivisor(int a, int b)
    {
        if (a < 0)
            throw new ArgumentOutOfRangeException(nameof(a));
        else if (b < 0)
            throw new ArgumentOutOfRangeException(nameof(b));
        while (a != 0 && b != 0)
        {
            if (a > b) a %= b;
            else       b %= a;
        }
        return a | b;
    }

    public static byte[] Pad(ReadOnlySpan<byte> data)
    {
        var length = GetPaddedLength(data.Length);
        var array = new byte[length];
        data.CopyTo(array);

        return array;
    }

    public static NativeMemoryArray<byte> PadCircularly(ReadOnlySpan<byte> data)
    {
        var length = GetPaddedLength(data.Length);
        var array = new NativeMemoryArray<byte>(length);

        data.CopyTo(array.AsSpan());

        if (data.Length < length)
        {
            var copied = data.Length;
            while (length > copied)
            {
                data[..Math.Min(data.Length, length - copied)].CopyTo(array.AsSpan(copied));
                copied += data.Length;
            }
        }

        return array;
    }

    public static void PadCircularly(ReadOnlySpan<byte> data, Span<byte> buffer)
    {
        if (data.Length > buffer.Length)
            throw new ArgumentException("Output buffer has smaller size than data.", nameof(buffer));

        data.CopyTo(buffer);

        if (data.Length < buffer.Length)
        {
            var copied = data.Length;
            while (buffer.Length > copied)
            {
                data[..Math.Min(data.Length, buffer.Length - copied)].CopyTo(buffer[copied..]);
                copied += data.Length;
            }
        }
    }

    public static Span<byte> GetBlockSpan(this MarshalMemoryStream stream, long offset)
    {
        var ahead = stream.Length - offset;

        if (ahead <= 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var alignedLength = (((ahead - 1) / LaneCount) + 1) * LaneCount;

        return alignedLength < int.MaxValue
            ? stream.AsSpan(offset, (int)alignedLength)
            : stream.AsSpan(offset, int.MaxValue / LaneCount * LaneCount);
    }
}
