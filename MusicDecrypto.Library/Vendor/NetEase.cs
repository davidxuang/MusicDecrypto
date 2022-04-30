using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using MusicDecrypto.Library.Cryptography.Extensions;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using MusicDecrypto.Library.Numerics;
using TagLib;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class NetEase : DecryptoBase
    {
        private static readonly byte[] _magic = { 0x43, 0x54, 0x45, 0x4E, 0x46, 0x44, 0x41, 0x4D };
        private static readonly byte[] _root = { 0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 };
        private static readonly byte[] _rootMeta = { 0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 0x26, 0x30, 0x55, 0x3C, 0x27, 0x28 };
        private static readonly byte[] _initBox = Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray();
        private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private Metadata? _metadata;
        private ImageTypes _coverType;
        private byte[]? _coverBuffer;

        public NetEase(MarshalMemoryStream buffer, string name) : base(buffer, name) { }

        protected override void Process()
        {
            // Check file header
            if (!Reader.ReadBytes(8).SequenceEqual(_magic))
                throw new InvalidDataException("File header is unexpected.");

            // Skip ahead
            _ = _buffer.Seek(2, SeekOrigin.Current);

            var mask = (stackalloc byte[Simd.GetPaddedLength(0x100)]);
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
                Simd.Align(mask, 0x100);
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
                _metadata = JsonSerializer.Deserialize<Metadata>(
                    meta, _serializerOptions);
                if (_metadata?.MusicName == null)
                    _metadata = JsonSerializer.Deserialize<RadioMetadata>(
                        meta, _serializerOptions).MainMusic;
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

            // Read music
            _buffer.Origin = _buffer.Position;
            int step = Simd.LaneCount;
            var data = _buffer.AsSimdPaddedSpan();
            int i_m;
            for (int i = 0; i < data.Length; i += step)
            {
                i_m = i % 0x100;
                var window = data[i..(i + step)];
                var v = new Vector<byte>(window);
                var m = new Vector<byte>(mask[i_m..(i_m + step)]);
                (m ^ v).CopyTo(window);
            }
        }

        protected override bool MetadataMisc(Tag tag)
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

            if (_metadata?.MusicName is string name)
            {
                if (tag.Title != name)
                {
                    tag.Title = name;
                    modified = true;
                }
            }
            var artists = _metadata?.Artist?.Select(tuple => tuple[0].ToString()!);
            if (artists?.Count() > 0 && tag.AlbumArtists.Length == 0)
            {
                tag.Performers = artists!.ToArray();
                tag.AlbumArtists = new[] { artists!.First() };
                modified = true;
            }
            if (_metadata?.Album is string album)
            {
                if (tag.Album != album)
                {
                    tag.Album = album;
                    modified = true;
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

        private struct Metadata
        {
            public string MusicName { get; set; }
            public object[][] Artist { get; set; }
            public string Album { get; set; }
            public string AlbumPic { get; set; }
        }

        private struct RadioMetadata
        {
            public Metadata MainMusic { get; set; }
        }

        public class NullFileChunkException : IOException
        {
            public NullFileChunkException(string message)
                : base(message) { }
        }
    }
}
