using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Numerics;

internal enum PaddingMode : byte
{
    Zero,
    Circular,
}

internal static class SimdHelper
{
    public static int LaneCount => Vector<byte>.Count;

    public static int GetPaddedLength(int length) =>
        (length % LaneCount == 0) ? length : length + LaneCount - GetCommonDivisor(length, LaneCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCommonDivisor(int a, int b)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(a);
        ArgumentOutOfRangeException.ThrowIfNegative(b);
        while (a != 0 && b != 0)
        {
            if (a > b) a %= b;
            else       b %= a;
        }
        return a | b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pad(ReadOnlySpan<byte> data, Span<byte> buffer, PaddingMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, data.Length, nameof(buffer));
        data.CopyTo(buffer);

        if (mode == PaddingMode.Circular)
        {
            var copied = data.Length;
            while (buffer.Length > copied)
            {
                data[..Math.Min(data.Length, buffer.Length - copied)].CopyTo(buffer[copied..]);
                copied += data.Length;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pad(Span<byte> buffer, int dataSize, PaddingMode mode = PaddingMode.Circular)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dataSize, buffer.Length);
        if (mode != PaddingMode.Circular) throw new NotSupportedException();

        var copied = dataSize;
        while (copied < buffer.Length)
        {
            buffer[..Math.Min(dataSize, buffer.Length - copied)].CopyTo(buffer[copied..]);
            copied += dataSize;
        }
    }
}
