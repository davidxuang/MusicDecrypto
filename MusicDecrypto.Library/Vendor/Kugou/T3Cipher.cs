using System;
using System.Numerics;
using System.Security.Cryptography;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

internal readonly struct T3Cipher : IDecryptor, IEncryptor
{
    private readonly NanoByteArray _slotKey;
    private const int _slotKeySize = MD5.HashSizeInBytes;
    private readonly NanoByteArray _fileKey;
    private const int _fileKeySize = MD5.HashSizeInBytes + 1;

    public T3Cipher(ReadOnlySpan<byte> slotKey, ReadOnlySpan<byte> fileKey)
    {
        var digest = (stackalloc byte[MD5.HashSizeInBytes + 1]);
        Hash(slotKey, digest);
        _slotKey = new(digest[..MD5.HashSizeInBytes], Numerics.PaddingMode.Circular);
        Hash(fileKey, digest);
        digest[^1] = 0x6b;
        _fileKey = new(digest, Numerics.PaddingMode.Circular);
    }

    private static void Hash(ReadOnlySpan<byte> data, Span<byte> digest)
    {
        MD5.HashData(data, digest);
        for (int i = 0; i < 8; i += 2)
        {
            (digest[i], digest[14 - i]) = (digest[14 - i], digest[i]);
            (digest[i + 1], digest[15 - i]) = (digest[15 - i], digest[i + 1]);
        }
    }

    public long Decrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        var mask = (stackalloc byte[step]);
        int offset_s, offset_f;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_s = (int)((offset + i) % _slotKeySize);
            offset_f = (int)((offset + i) % _fileKeySize);
            var window = data[i..(i + step)];
            GetOffsetMask(mask, offset + i);
            var v = new Vector<byte>(window);
            var f = new Vector<byte>(_fileKey[offset_f..(offset_f + step)]);
            var s = new Vector<byte>(_slotKey[offset_s..(offset_s + step)]);
            var m = new Vector<byte>(mask);
            var x = v ^ f;
            (x ^ (x << 4) ^ s ^ m).CopyTo(window);
        }
        return data.Length;
    }

    public long Encrypt(Span<byte> data, long offset)
    {
        var step = SimdHelper.LaneCount;
        var mask = (stackalloc byte[step]);
        int offset_s, offset_f;
        for (int i = 0; i < data.Length; i += step)
        {
            offset_s = (int)((offset + i) % _slotKeySize);
            offset_f = (int)((offset + i) % _fileKeySize);
            var window = data[i..(i + step)];
            GetOffsetMask(mask, offset + i);
            var v = new Vector<byte>(window);
            var f = new Vector<byte>(_fileKey[offset_f..(offset_f + step)]);
            var s = new Vector<byte>(_slotKey[offset_s..(offset_f + step)]);
            var m = new Vector<byte>(mask);
            var x = v ^ s ^ m;
            (x ^ (x << 4) ^ f).CopyTo(window);
        }
        return data.Length;
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
