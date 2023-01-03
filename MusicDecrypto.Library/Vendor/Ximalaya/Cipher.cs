using System;
using System.Numerics;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Ximalaya;

internal sealed class Cipher : IDecryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _key;
    private readonly int _keySize;
    private readonly ushort[] _map;

    public Cipher(ReadOnlySpan<byte> key, ushort[] map)
    {
        _key = SimdHelper.PadCircularly(key);
        _keySize = key.Length;
        _map = map;
    }

    public void Dispose()
    {
        _key.Dispose();
    }

    public long Decrypt(Span<byte> data, long offset)
    {
        if (offset == 0)
        {
            var buffer = (stackalloc byte[SimdHelper.GetPaddedLength(_map.Length)]);

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = data[_map[i]];
            }

            int step = SimdHelper.LaneCount;
            int i_k;
            for (int i = 0; i < buffer.Length; i += step)
            {
                i_k = (int)((offset + i) % _keySize);
                var window = buffer[i..(i + step)];
                var v = new Vector<byte>(window);
                var k = new Vector<byte>(_key.AsSpan(i_k, step));
                (v ^ k).CopyTo(window);
            }

            buffer[.._map.Length].CopyTo(data);
        }
        return offset + data.Length;
    }
}
