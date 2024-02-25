using System;
using System.IO;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library.Vendor.Kugou;

/// <summary>
/// Header format
/// <code>
/// 0x00 .. 0x10    magic
/// 0x10    int32   offset
/// 0x14    int32   encryption_type
/// 0x18    int32   key_slot
/// 0x1c .. 0x2c    file_signature
/// 0x2c .. 0x3c    file_key
/// 0x3c .. offset  padding
/// </code>
/// </summary>
internal sealed partial class Decrypto : DecryptoBase
{
    private static readonly byte[] _slotKey = "l,/'"u8.ToArray();
    private static readonly byte[] _kgmSignature = [0x38, 0x85, 0xed, 0x92, 0x79, 0x5f, 0xf8, 0x4c, 0xb3, 0x03, 0x61, 0x41, 0x16, 0xa0, 0x1d, 0x47];
    private static readonly byte[] _vprSignature = [0x1d, 0x5a, 0x05, 0x34, 0x0c, 0x41, 0x8d, 0x42, 0x9c, 0x83, 0x92, 0x6c, 0xae, 0x16, 0xfe, 0x56];

    private enum Subtypes : byte
    {
        KGM,
        VPR
    }

    protected override IDecryptor Decryptor { get; init; }

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
    {
        var subtype = _buffer.AsSpan() switch
        {
            [0x7c, 0xd5, 0x32, 0xeb, 0x86, 0x02, 0x7f, 0x4b, 0xa8, 0xaf, 0xa6, 0x8e, 0x0f, 0xff, 0x99, 0x14, ..] => Subtypes.KGM,
            [0x05, 0x28, 0xbc, 0x96, 0xe9, 0xe4, 0x5a, 0x43, 0x91, 0xaa, 0xbd, 0xd0, 0x7a, 0xf5, 0x36, 0x31, ..] => Subtypes.VPR,
            _ => ThrowInvalidData.True<Subtypes>("File header")
        };

        _ = buffer.Seek(0x10, SeekOrigin.Begin);
        var offset = _reader.ReadInt32();
        var type = _reader.ReadInt32();

        var slot = _reader.ReadInt32();
        if (slot != 1)
            throw new NotImplementedException();

        NanoByteArray signature = new();
        ThrowInvalidData.IfLessThan(_reader.Read(signature[..0x10]), 0x10, "File signature");

        var fileKey = (stackalloc byte[0x10]);
        ThrowInvalidData.IfLessThan(_reader.Read(fileKey), 0x10, "File key");

        Decryptor = type switch
        {
            2 => new T2Cipher(_slotKey),
            3 => new T3Cipher(_slotKey, fileKey),
            4 => throw new NotImplementedException(),
            _ => throw new NotSupportedException(),
        };

        Decryptor.Decrypt(signature[..SimdHelper.LaneCount], 0);
        switch (subtype)
        {
            case Subtypes.KGM:
                ThrowInvalidData.If(!MemoryExtensions.SequenceEqual<byte>(_kgmSignature, signature[..0x10]), "File signature");
                break;

            case Subtypes.VPR:
                ThrowInvalidData.If(!MemoryExtensions.SequenceEqual<byte>(_vprSignature, signature[..0x10]), "File signature");
                break;

            default:
                throw new NotSupportedException();
        }

        _buffer.Origin = offset;
    }
}
