using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicDecrypto.Library.Cryptography.Extensions;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using TagLib;

namespace MusicDecrypto.Library.Vendor.NetEase;

internal sealed partial class Decrypto : DecryptoBase
{
    private static readonly byte[] _magic = "CTENFDAM"u8.ToArray();
    private static readonly byte[] _root = "hzHRAmso5kInbaxW"u8.ToArray();
    private static readonly byte[] _rootMeta = @"#14ljk_!\]&0U<'("u8.ToArray();
    private static readonly byte[] _initBox = Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray();

    private readonly Metadata? _metadata;
    private byte[]? _coverBuffer;

    protected override IDecryptor Decryptor { get; init; }

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
    {
        // Check file header
        ThrowInvalidData.If(!MemoryExtensions.SequenceEqual<byte>(_reader.ReadBytes(8), _magic), "File header");

        // Skip ahead
        _ = _buffer.Seek(2, SeekOrigin.Current);

        var mask = (stackalloc byte[Cipher.MaskSize]);
        try
        {
            // Read key
            var chunkLength = _reader.ReadInt32();
            var keyChunk = (stackalloc byte[chunkLength]);
            ReadChunk(keyChunk, 0x64);
            var keyBuffer = (stackalloc byte[chunkLength]);
            var decryptLength = ((ReadOnlySpan<byte>)keyChunk).AesEcbDecrypt(_root, keyBuffer);
            var key = keyBuffer[0x11..decryptLength];
            var keyLength = key.Length;

            // Build key box
            var box = (stackalloc byte[0x100]);
            _initBox.CopyTo(box);

            for (int i = 0, j = 0; i < 0x100; i++)
            {
                j = (byte)(box[i] + j + key[i % keyLength]);
                (box[j], box[i]) = (box[i], box[j]);
            }

            // Build mask
            for (int i = 0; i < 0x100; i++)
            {
                var j = (byte)(i + 1);
                mask[i] = box[(byte)(box[j] + box[(byte)(box[j] + j)])];
            }
            Decryptor = new Cipher(mask);
        }
        catch (IOException e)
        {
            throw new InvalidDataException("Key chunk is corrupted.", e);
        }

        try
        {
            // Read metadata
            var metaChunk = (stackalloc byte[_reader.ReadInt32()]);
            ReadChunk(metaChunk, 0x63);
            int skipCount = 0x16;
            for (int i = 0; i < metaChunk.Length; i++)
            {
                if (metaChunk[i] == 0x3a)
                {
                    skipCount = i + 1;
                    break;
                }
            }

            // Resolve metadata
            var metaString = Encoding.ASCII.GetString(metaChunk[skipCount..]);
            var metaCipher = (stackalloc byte[metaString.Length / 4 * 3 + 2]);
            ThrowInvalidData.If(!Convert.TryFromBase64String(metaString, metaCipher, out var cipherLength), "File meta");
            var metaBuffer = (stackalloc byte[cipherLength]);
            var metaLength = ((ReadOnlySpan<byte>)metaCipher[..cipherLength]).AesEcbDecrypt(_rootMeta, metaBuffer);
            string meta = Encoding.UTF8.GetString(metaBuffer[..metaLength]);
            _metadata = JsonSerializer.Deserialize(meta[6..], NetEaseSerializerContext.Default.Metadata);
            if (_metadata?.MusicName is null)
                _metadata = JsonSerializer.Deserialize(meta[6..], NetEaseSerializerContext.Default.RadioMetadata)?.MainMusic;
        }
        catch (IOException)
        {
            RaiseWarn("File does not contain metadata.");
        }
        catch
        {
            RaiseWarn("Metadata seems corrupted.");
        }

        // Skip ahead
        _ = _buffer.Seek(9, SeekOrigin.Current);

        // Read cover data
        try
        {
            _coverBuffer = new byte[_reader.ReadInt32()];
            ReadChunk(_coverBuffer);
        }
        catch (IOException)
        {
            _coverBuffer = null;
            RaiseWarn("File does not contain cover image. Will try to get from server.");
        }

        // Set offset
        _buffer.Origin = _buffer.Position;
    }

    protected override async ValueTask<bool> ProcessMetadataOverrideAsync(Tag tag)
    {
        if (tag is null) return false;

        bool modified = false;

        if (_coverBuffer is null)
        {
            try
            {
                var coverUri = _metadata?.AlbumPic;
                if (!Uri.IsWellFormedUriString(coverUri, UriKind.Absolute))
                {
                    RaiseWarn("File does not contain cover link.");
                    throw new InvalidDataException();
                }
                using var httpClient = new HttpClient();
                _coverBuffer = await httpClient.GetByteArrayAsync(coverUri);
            }
            catch
            {
                RaiseWarn("Failed to download cover image.");
            }
        }

        var coverType = _coverBuffer is null ? ImageType.Undefined : _coverBuffer.AsSpan().SniffImageType();
        if (coverType == ImageType.Undefined)
        {
            _coverBuffer = null;
        }
        else
        {
            if (tag.Pictures.Length > 0)
            {
                if (tag.Pictures[0].Type != PictureType.FrontCover)
                {
                    tag.Pictures[0].Type = PictureType.FrontCover;
                    modified = true;
                }
            }
            else
            {
                tag.Pictures =
                [
                    new Picture(new ByteVector(_coverBuffer))
                    {
                        MimeType = coverType.GetMime(),
                        Type = PictureType.FrontCover
                    }
                ];
                modified = true;
            }
        }

        if (_metadata is not null)
        {
            if (!string.IsNullOrEmpty(_metadata.MusicName))
            {
                if (tag.Title != _metadata.MusicName)
                {
                    tag.Title = _metadata.MusicName;
                    modified = true;
                }
            }
            if (_metadata.Artists?.Any() == true)
            {
                tag.Performers = _metadata.Artists.ToArray();
                modified = true;
            }
            if (!string.IsNullOrEmpty(_metadata.Album))
            {
                if (tag.Album != _metadata.Album)
                {
                    tag.Album = _metadata.Album;
                    modified = true;
                }
            }
        }

        return modified;
    }

    private void ReadChunk(Span<byte> chunk, byte obfuscator)
    {
        if (chunk.Length > 0)
        {
            _buffer.Read(chunk);
            for (int i = 0; i < chunk.Length; i++)
                chunk[i] ^= obfuscator;
        }
        else
        {
            throw new IOException();
        }
    }

    private void ReadChunk(Span<byte> chunk)
    {
        if (chunk.Length > 0)
        {
            _buffer.Read(chunk);
        }
        else
        {
            throw new IOException();
        }
    }

    private sealed class Metadata
    {
        public string? MusicName { get; set; }
        [JsonInclude]
        public IEnumerable<IEnumerable<object?>>? Artist
        {
            get => Artists?.Select(x => new[] { x, null }); // should not be used
            set
            {
                Artists = value?.Select(x => x?.FirstOrDefault()?.ToString());
            }
        }
        [JsonIgnore]
        public IEnumerable<string?>? Artists { get; private set; }
        public string? Album { get; set; }
        public string? AlbumPic { get; set; }
    }

    private sealed class RadioMetadata
    {
        public Metadata? MainMusic { get; set; }
    }

    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Metadata))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(RadioMetadata))]
    private sealed partial class NetEaseSerializerContext : JsonSerializerContext { }
}
