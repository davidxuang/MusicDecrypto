using System;
using System.IO;
using System.Linq;
using System.Text;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;

namespace MusicDecrypto.Library.Vendor.Xiami;

internal sealed class Decrypto : DecryptoBase
{
    private static readonly byte[] _magic = "ifmt"u8.ToArray();
    private static readonly byte[] _separator = [0xfe, 0xfe, 0xfe, 0xfe];

    protected override IDecryptor Decryptor { get; init; }

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type = AudioTypes.Undefined)
        : base(buffer, name, warn, null, type)
    {
        // Check file header
        if (!_reader.ReadBytes(8).AsSpan(0, 4).SequenceEqual(_magic) || !_reader.ReadBytes(4).SequenceEqual(_separator))
        {
            throw new InvalidDataException(
                buffer.AsSpan().SniffAudioType() == AudioTypes.Undefined
                ? "File header is unexpected."
                : "File seems unencrypted.");
        }

        _ = _buffer.Seek(4, SeekOrigin.Begin);
        var identifier = Encoding.ASCII.GetString(_reader.ReadBytes(4));
        if (_audioType == AudioTypes.Undefined) _audioType = identifier switch
        {
            " A4M" => AudioTypes.XM4a,
            "FLAC" => AudioTypes.Flac,
            " MP3" => AudioTypes.Mpeg,
            " WAV" => AudioTypes.XWav,
            _ => throw new InvalidDataException("Unable to determine media format."),
        };

        _ = _buffer.Seek(12, SeekOrigin.Begin);
        _startOffset = _reader.ReadByte() | _reader.ReadByte() << 8 | _reader.ReadByte() << 16;

        Decryptor = new Cipher(_reader.ReadByte());
        _buffer.Origin = 0x10;
    }
}
