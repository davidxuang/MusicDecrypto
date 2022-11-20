using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicDecrypto.Library.Cryptography.Extensions;
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
    private readonly ImageTypes _coverType;
    private readonly byte[]? _coverBuffer;
    private readonly IDecryptor _cipher;
    protected override IDecryptor Decryptor => _cipher;

    public Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn) : base(buffer, name, warn)
    {

        // Check file header
        if (!Reader.ReadBytes(8).SequenceEqual(_magic))
            throw new InvalidDataException("File header is unexpected.");

        // Skip ahead
        _ = _buffer.Seek(2, SeekOrigin.Current);

        var mask = new byte[0x100];
        try
        {
            // Read key
            var keyChunk = (stackalloc byte[Reader.ReadInt32()]);
            ReadChunk(keyChunk, 0x64);
            var key = keyChunk.ToArray().AesEcbDecrypt(_root).AsSpan(0x11);
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
            _cipher = new Cipher(mask);
        }
        catch (NullFileChunkException e)
        {
            throw new InvalidDataException("Key chunk is corrupted.", e);
        }

        try
        {
            // Read metadata
            var metaChunk = (stackalloc byte[Reader.ReadInt32()]);
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
            string meta = Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    Encoding.ASCII.GetString(
                        metaChunk[skipCount..]))
                .AesEcbDecrypt(_rootMeta)[6..]);
            _metadata = JsonSerializer.Deserialize(meta, SerializerContext.Default.Metadata);
            if (_metadata?.MusicName == null)
                _metadata = JsonSerializer.Deserialize(meta, SerializerContext.Default.RadioMetadata)?.MainMusic;
        }
        catch (NullFileChunkException)
        {
            RaiseWarn("File does not contain metadata.");
        }
        catch
        {
            RaiseWarn("Metadata seems corrupted.");
        }

        // Skip ahead
        _ = _buffer.Seek(9, SeekOrigin.Current);

        // Get cover data
        try
        {
            // Plan A: Read cover from file
            _coverBuffer = new byte[Reader.ReadInt32()];
            ReadChunk(_coverBuffer);
        }
        catch (NullFileChunkException)
        {
            RaiseWarn("File does not contain cover image. Trying to get from server...");

            // Plan B: get image from server
            try
            {
                var coverUri = _metadata?.AlbumPic;
                if (!Uri.IsWellFormedUriString(coverUri, UriKind.Absolute))
                {
                    RaiseWarn("File does not contain cover link.");
                    throw;
                }
                using var httpClient = new HttpClient();
                _coverBuffer = httpClient.GetByteArrayAsync(coverUri).Result;
            }
            catch
            {
                RaiseWarn("Failed to download cover image.");
            }
        }
        _coverType = _coverBuffer?.SniffImageType() ?? ImageTypes.Undefined;
        if (_coverType == ImageTypes.Undefined) _coverBuffer = null;

        // Set offset
        _buffer.Origin = _buffer.Position;
    }

    protected override bool ProcessMetadataOverride(Tag tag)
    {
        if (tag == null) return false;

        bool modified = false;

        if (_coverType != ImageTypes.Undefined)
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
                tag.Pictures = new IPicture[] {
                    new Picture(new ByteVector(_coverBuffer))
                    {
                        MimeType = _coverType.GetMime(),
                        Type = PictureType.FrontCover
                    }
                };
                modified = true;
            }
        }

        if (_metadata != null)
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
                if (tag.AlbumArtists.Length == 0)
                {
                    tag.AlbumArtists = new[] { _metadata.Artists.First() };
                }
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
            throw new NullFileChunkException("Failed to load file chunk.");
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
            throw new NullFileChunkException("Failed to load file chunk.");
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

    public class NullFileChunkException : IOException
    {
        public NullFileChunkException(string message)
            : base(message) { }
    }

    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Metadata))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(RadioMetadata))]
    private sealed partial class SerializerContext : JsonSerializerContext { }
}