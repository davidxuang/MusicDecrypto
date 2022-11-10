using System;
using System.Numerics;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

internal sealed class T2Cipher : IDecryptor, IEncryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _slotKey;
    private readonly int _slotKeySize;

    public T2Cipher(ReadOnlySpan<byte> slotKey)
    {
        _slotKey = SimdHelper.PadCircularly(slotKey);
        _slotKeySize = slotKey.Length;
    }

    public void Dispose()
    {
        _slotKey.Dispose();
    }

    public void Decrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        int i_s;
        for (int i = 0; i < data.Length; i += step)
        {
            i_s = (int)((offset + i) % _slotKeySize);
            var window = data[i..(i + step)];
            var v = new Vector<byte>(window);
            var s = new Vector<byte>(_slotKey.AsSpan(i_s, step));
            // blocked by upstream: (x * 0x10) -> (x << 4)
            (v ^ (v * 0x10) ^ s).CopyTo(window);
        }
    }

    public void Encrypt(Span<byte> data, long offset)
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
            // blocked by upstream: (x * 0x10) -> (x << 4)
            (x ^ (x * 0x10)).CopyTo(window);
        }
    }
}
