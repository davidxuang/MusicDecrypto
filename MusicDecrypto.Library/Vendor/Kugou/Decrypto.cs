using System;
using System.IO;

namespace MusicDecrypto.Library.Vendor.Kugou;

/// <summary>
/// Header format
/// <code>
/// 0x00 .. 0x10    magic
/// 0x10    int32   offset
/// 0x14    int32   encryption_type
/// 0x18    int32   key_slot
/// 0x1c .. 0x1c    file_signature
/// 0x2c .. 0x3c    file_key
/// 0x3c .. offset  padding
/// </code>
/// </summary>
internal sealed partial class Decrypto : DecryptoBase
{
    private readonly IDecryptor _decryptor;
    protected override IDecryptor Decryptor => _decryptor;

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
    {
        _ = _buffer.Seek(0x18, SeekOrigin.Begin);
        var slot = Reader.ReadInt32();
        if (slot != 1)
            throw new NotImplementedException();
        var slotKey = (stackalloc byte[] { 0x6C, 0x2C, 0x2F, 0x27 });

        _ = _buffer.Seek(0x2c, SeekOrigin.Begin);
        var fileKey = Reader.ReadBytes(0x10);

        _ = buffer.Seek(0x14, SeekOrigin.Begin);
        var type = Reader.ReadInt32();
        _decryptor = type switch
        {
            2 => new T2Cipher(slotKey),
            3 => new T3Cipher(slotKey, fileKey),
            4 => throw new NotImplementedException(),
            _ => throw new NotSupportedException(),
        };

        _ = _buffer.Seek(0x10, SeekOrigin.Begin);
        _buffer.Origin = Reader.ReadInt32();
    }
}
