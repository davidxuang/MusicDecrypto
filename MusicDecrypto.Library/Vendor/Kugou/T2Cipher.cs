using System;
using System.Numerics;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

internal readonly struct T2Cipher(ReadOnlySpan<byte> slotKey) : IDecryptor, IEncryptor
{
    private readonly NanoByteArray _slotKey = new(slotKey, PaddingMode.Circular);
    private readonly int _slotKeySize = slotKey.Length;

    public long Decrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        int offset_s;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_s = (int)((offset + i) % _slotKeySize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var s = new Vector<byte>(_slotKey[offset_s..(offset_s + step)]);
            (v ^ (v << 4) ^ s).CopyTo(window);
        }
        return data.Length;
    }

    public long Encrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        int offset_s;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_s = (int)((offset + i) % _slotKeySize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var s = new Vector<byte>(_slotKey[offset_s..(offset_s + step)]);
            var x = v * s;
            (x ^ (x << 4)).CopyTo(window);
        }
        return data.Length;
    }
}
