using System;
using System.Numerics;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

internal sealed class T2Cipher(ReadOnlySpan<byte> slotKey) : IDecryptor, IEncryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _slotKey = SimdHelper.PadCircularly(slotKey);
    private readonly int _slotKeySize = slotKey.Length;

    public void Dispose()
    {
        _slotKey.Dispose();
    }

    public long Decrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        int i_s;
        for (int i = 0; i < data.Length; i += step)
        {
            i_s = (int)((offset + i) % _slotKeySize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var s = new Vector<byte>(_slotKey.AsSpan(i_s, step));
            (v ^ (v << 4) ^ s).CopyTo(window);
        }
        return offset + data.Length;
    }

    public long Encrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        int i_s;
        for (int i = 0; i < data.Length; i += step)
        {
            i_s = (int)((offset + i) % _slotKeySize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var s = new Vector<byte>(_slotKey.AsSpan(i_s, step));
            var x = v * s;
            (x ^ (x << 4)).CopyTo(window);
        }
        return offset + data.Length;
    }
}
