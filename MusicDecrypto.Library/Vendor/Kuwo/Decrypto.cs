using System;
using System.IO;
using System.Text;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kuwo;

internal sealed class Decrypto : DecryptoBase
{
    private static readonly byte[] _magic = "yeelion-kuwo"u8.ToArray(); // "yeelion-kuwo-tme"
    private static readonly byte[] _root = "MoOtOiTvINGwd2E6n0E1i7L5t2IoOoNk"u8.ToArray();
    private static readonly int _paddedMaskSize = SimdHelper.GetPaddedLength(0x20);

    protected override IDecryptor Decryptor { get; init; }

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
    {
        ThrowInvalidData.IfLessThan(_buffer.Length, 1024, "File");

        var cache = (stackalloc byte[0x10]);
        _reader.Read(cache);
        ThrowInvalidData.If(!MemoryExtensions.SequenceEqual(cache[.._magic.Length], _magic), "File header");

        _ = _buffer.Seek(8, SeekOrigin.Current);

        var mask = (stackalloc byte[Cipher.MaskSize]);
        var seed = _reader.ReadUInt32().ToString();
        var seedLength = Encoding.ASCII.GetBytes(seed.AsSpan(0, Math.Min(Cipher.MaskSize, seed.Length)), mask);
        SimdHelper.Pad(mask, seedLength);

        for (int i = 0; i < Cipher.MaskSize; i++)
        {
            mask[i] ^= _root[i];
        }

        Decryptor = new Cipher(mask);
        _buffer.Origin = 0x400;
    }
}
