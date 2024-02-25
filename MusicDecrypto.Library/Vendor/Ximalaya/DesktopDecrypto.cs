using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Numerics;
using TagLib;
using static MusicDecrypto.Library.Media.ID3v2.Frame.FrameHeader;

namespace MusicDecrypto.Library.Vendor.Ximalaya;

internal sealed class DesktopDecrypto : DecryptoBase
{
    private readonly string? _album;
    private readonly string? _title;
    private readonly string? _performer;

    public DesktopDecrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioType type = AudioType.Undefined)
        : base(buffer, name, warn, type)
    {
        ThrowInvalidData.IfLessThan(buffer.Length, 10, "ID3 header");

        int headerSize = BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(6));
        ThrowInvalidData.IfNegative(headerSize, "ID3 header length");
        int origin = headerSize + 10;
        ThrowInvalidData.IfLessThan(buffer.Length, origin, "ID3 header");

        var tag = new ID3v2.Tag(buffer.AsSpan());
        foreach (var frame in tag)
        {
            if (frame.Header.Id == FrameId.TALB)
            {
                _album = frame.Text;
            }
            else if (frame.Header.Id == FrameId.TIT2)
            {
                _title = frame.Text;
            }
            else if (frame.Header.Id == FrameId.TPE1)
            {
                _performer = frame.Text;
            }
        }
        Decryptor = new Cipher(tag);
        _buffer.Origin = tag.Size;
    }

    protected override ValueTask<bool> ProcessMetadataOverrideAsync(Tag tag)
    {
        var modified = false;
        if (string.IsNullOrEmpty(tag.Album))
        {
            tag.Album = _album;
            modified = true;
        }
        if (string.IsNullOrEmpty(tag.Title))
        {
            tag.Title = _title;
            modified = true;
        }
        if (string.IsNullOrEmpty(tag.Performers.FirstOrDefault()))
        {
            tag.Performers = [_performer];
            modified = true;
        }
        return ValueTask.FromResult(modified);
    }

    protected override IDecryptor Decryptor { get; init; }
}

file struct Cipher : IDecryptor
{
    const int _ivSize = 16;
    const int _key2Size = 24;

    readonly long IDecryptor.TrimStart => _trimStart;

    private static readonly byte[] _key1 = "ximalayaximalayaximalayaximalaya"u8.ToArray();
    private static readonly byte[] _key2i = "123456781234567812345678"u8.ToArray();

    private readonly int _size;
    private readonly byte[] _iv1;
    private readonly byte[] _key2;
    private readonly NanoByteArray _header;
    private readonly int _headerSize;

    private long _trimStart;

#pragma warning disable CS8618
    public Cipher(ID3v2.Tag tag)
    {
        foreach (var frame in tag)
        {
            if (frame.Header.Id == FrameId.TSIZ)
            {
                _size = int.Parse(frame.Text);
            }
            else if (frame.Header.Id == FrameId.TSSE)
            {
                ThrowInvalidData.If(
                    !Convert.TryFromBase64String(frame.Text, _header, out _headerSize),
                    "File header");
            }
            else if (frame.Header.Id == FrameId.TSRC || frame.Header.Id == FrameId.TENC)
            {
                _iv1 = Convert.FromHexString(frame.Text);
                ThrowInvalidData.IfNotEqual(_iv1.Length, _ivSize, "File IV");
            }
            else if (frame.Header.Id == FrameId.TRCK)
            {
                _key2 = new byte[_key2Size];
                _key2i.CopyTo(_key2.AsSpan());
                var text = frame.Text;
                var textSize = Math.Min(text.Length, _key2Size);
                ThrowInvalidData.IfNotEqual(
                    Encoding.Latin1.GetBytes(text.AsSpan(0, textSize), _key2.AsSpan(_key2Size - textSize)),
                    textSize,
                    "File key");
                ;
            }
        }
        ThrowInvalidData.IfNull(_iv1, "File IV");
        ThrowInvalidData.IfNull(_key2, "File Key");
    }
#pragma warning restore CS8618

    public long Decrypt(Span<byte> data, long offset)
    {
        if (offset > _size)
        {
            return long.MaxValue;
        }

        ArgumentOutOfRangeException.ThrowIfNotEqual(offset, 0, nameof(offset));

        int size;
        data = data[.._size];
        var buffer = data;

        using var aes = Aes.Create();
        aes.Key = _key1;
        size = aes.DecryptCbc(buffer, _iv1, buffer, System.Security.Cryptography.PaddingMode.PKCS7);
        buffer = buffer[..size];
        Base64.DecodeFromUtf8(buffer, buffer, out var d, out size);
        buffer = buffer[..size];

        aes.Key = _key2;
        size = aes.DecryptCbc(buffer, _key2.AsSpan(0, 0x10), buffer, System.Security.Cryptography.PaddingMode.PKCS7);
        buffer = buffer[..size];
        Base64.DecodeFromUtf8(buffer[..size], buffer, out _, out size);
        buffer = buffer[..size];

        buffer.CopyTo(data[^size..]);
        _header[.._headerSize].CopyTo(data[^(size + _headerSize)..]);
        _trimStart = data.Length - size - _headerSize;

        return long.MaxValue;
    }
}
