using System;
using System.IO;
using System.Linq;
using System.Text;
using MusicDecrypto.Library.Media;

namespace MusicDecrypto.Library.Vendor.Xiami;

internal sealed class Decrypto : DecryptoBase
{
    private static readonly byte[] _magic = "ifmt"u8.ToArray();
    private static readonly byte[] _separator = { 0xfe, 0xfe, 0xfe, 0xfe };

    private readonly IDecryptor _cipher;
    protected override IDecryptor Decryptor => _cipher;

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type = AudioTypes.Undefined)
        : base(buffer, name, warn, type)
    {
        // Check file header
        if (!Reader.ReadBytes(8).AsSpan(0, 4).SequenceEqual(_magic) || !Reader.ReadBytes(4).SequenceEqual(_separator))
        {
            throw new InvalidDataException(
                Path.GetExtension(Name).TrimStart('.') == "xm"
                ? "File header is unexpected."
                : "File seems unencrypted.");
        }

        _ = _buffer.Seek(4, SeekOrigin.Begin);
        var identifier = Encoding.ASCII.GetString(Reader.ReadBytes(4));
        if (_audioType == AudioTypes.Undefined) _audioType = identifier switch
        {
            " A4M" => AudioTypes.Mp4,
            "FLAC" => AudioTypes.Flac,
            " MP3" => AudioTypes.Mpeg,
            " WAV" => AudioTypes.XWav,
            _ => throw new InvalidDataException("Unable to determine media format."),
        };

        _ = _buffer.Seek(12, SeekOrigin.Begin);
        _startOffset = Reader.ReadByte() | Reader.ReadByte() << 8 | Reader.ReadByte() << 16;

        _cipher = new Cipher(Reader.ReadByte());
        _buffer.Origin = 0x10;
    }
}
