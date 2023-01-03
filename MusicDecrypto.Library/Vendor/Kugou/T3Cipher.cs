using System;
using System.Numerics;
using System.Security.Cryptography;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

internal sealed class T3Cipher : IDecryptor, IEncryptor, IDisposable
{
    private readonly NativeMemoryArray<byte> _slotKey;
    private const int _slotKeySize = 0x10;
    private readonly NativeMemoryArray<byte> _fileKey;
    private const int _fileKeySize = 0x11;

    public T3Cipher(ReadOnlySpan<byte> slotKey, ReadOnlySpan<byte> fileKey)
    {
        _slotKey = SimdHelper.PadCircularly(Hash(slotKey));
        var key = (stackalloc byte[0x11]);
        Hash(fileKey).CopyTo(key);
        key[0x10] = 0x6b;
        _fileKey = SimdHelper.PadCircularly(key);
    }

    public void Dispose()
    {
        _slotKey.Dispose();
        _fileKey.Dispose();
    }

    private static byte[] Hash(ReadOnlySpan<byte> data)
    {
        var digest = MD5.HashData(data);
        for (int i = 0; i < 8; i += 2)
        {
            (digest[i], digest[14 - i]) = (digest[14 - i], digest[i]);
            (digest[i + 1], digest[15 - i]) = (digest[15 - i], digest[i + 1]);
        }
        return digest;
    }

    public long Decrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        var mask = (stackalloc byte[step]);
        int i_s, i_f;
        for (int i = 0; i < data.Length; i += step)
        {
            i_s = (int)((offset + i) % _slotKeySize);
            i_f = (int)((offset + i) % _fileKeySize);
            var window = data[i..(i + step)];
            GetOffsetMask(mask, offset + i);
            var v = new Vector<byte>(window);
            var f = new Vector<byte>(_fileKey.AsSpan(i_f, step));
            var s = new Vector<byte>(_slotKey.AsSpan(i_s, step));
            var m = new Vector<byte>(mask);
            var x = v ^ f;
            // blocked by upstream: (x * 0x10) -> (x << 4)
            (x ^ (x * 0x10) ^ s ^ m).CopyTo(window);
        }
        return offset + data.Length;
    }

    public long Encrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        var mask = (stackalloc byte[step]);
        int i_s, i_f;
        for (int i = 0; i < data.Length; i += step)
        {
            i_s = (int)((offset + i) % _slotKeySize);
            i_f = (int)((offset + i) % _fileKeySize);
            var window = data[i..(i + step)];
            GetOffsetMask(mask, offset + i);
            var v = new Vector<byte>(window);
            var f = new Vector<byte>(_fileKey.AsSpan(i_f, step));
            var s = new Vector<byte>(_slotKey.AsSpan(i_s, step));
            var m = new Vector<byte>(mask);
            var x = v ^ s ^ m;
            // blocked by upstream: (x * 0x10) -> (x << 4)
            (x ^ (x * 0x10) ^ f).CopyTo(window);
        }
        return offset + data.Length;
    }

    private static void GetOffsetMask(Span<byte> mask, long offset)
    {
        for (int i = 0; i < mask.Length; i++)
        {
            var j = offset + i;
            mask[i] = (byte)(j ^ (j >> 8) ^ (j >> 16) ^ (j >> 24));
        }
    }
}
