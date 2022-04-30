using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Numerics
{
    internal static class Simd
    {
        internal static int LaneCount => Vector<byte>.Count;

        internal static int GetPaddedLength(int length) =>
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

        internal static void Align(Span<byte> buffer, int length)
        {
            if (length % LaneCount == 0)
                return;

            if (buffer.Length != GetPaddedLength(length))
                throw new ArgumentException("Length of array alignment buffer is incorrect.", nameof(buffer));

            var copied = length;
            while (buffer.Length > copied)
            {
                buffer[..Math.Min(length, buffer.Length - copied)].CopyTo(buffer[copied..]);
                copied += length;
            }
        }

        internal static byte[] Align(ReadOnlySpan<byte> input)
        {
            var length = GetPaddedLength(input.Length);
            if (input.Length == length)
            {
                return input.ToArray();
            }
            else
            {
                var array = new byte[length];
                input.CopyTo(array.AsSpan());
                var copied = input.Length;
                while (length > copied)
                {
                    input[..Math.Min(input.Length, length - copied)].CopyTo(array.AsSpan(copied));
                    copied += input.Length;
                }
                return array;
            }
        }
    }
}
