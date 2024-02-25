using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Numerics.Synchsafe;

[InlineArray(2)]
internal struct Int16S
{
#pragma warning disable IDE0044,IDE0051
    private byte _byte;
#pragma warning restore IDE0044, IDE0051

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator short(Int16S s) => (short)((s[0] << 7) + s[1]);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ushort(Int16S s) => (ushort)((s[0] << 7) + s[1]);

    public override readonly string ToString() => ((ushort)this).ToString();
    public override readonly int GetHashCode() => ((ushort)this).GetHashCode();
}
