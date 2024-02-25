using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Numerics.Synchsafe;

[InlineArray(4)]
internal struct Int32S
{
#pragma warning disable IDE0044,IDE0051
    private byte _byte;
#pragma warning restore IDE0044, IDE0051

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(Int32S s) => (s[0] << 21) + (s[1] << 14) + (s[2] << 7) + s[3];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(Int32S s) => ((uint)s[0] << 21) + ((uint)s[1] << 14) + ((uint)s[2] << 7) + s[3];

    public override readonly string ToString() => ((uint)this).ToString();
    public override readonly int GetHashCode() => ((uint)this).GetHashCode();
}
