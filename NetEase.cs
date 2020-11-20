using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace MusicDecrypto
{
    public sealed class NetEaseDecrypto : Decrypto
    {
        private static readonly byte[] _rootKey = { 0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 };
        private static readonly byte[] _jsonKey = { 0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 0x26, 0x30, 0x55, 0x3C, 0x27, 0x28 };
        private readonly byte[] _mainKey = Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray();
        private Metadata? _metadata;

        public NetEaseDecrypto(FileInfo file) : base(file) { }

        protected override void PreDecrypt()
        {
            // Check file header
            if (_reader.ReadUInt64() != 0x4d4144464e455443)
                throw new DecryptoException("File header is unexpected.", _input.FullName);

            // Skip ahead
            _ = _buffer.Seek(2, SeekOrigin.Current);
        }

        protected override void Decrypt()
        {
            try
            {
                // Read key
                byte[] key = ReadIndexedChunk(0x64).AesEcbDecrypt(_rootKey);

                // Build main key
                for (uint trgIndex = 0, swpIndex, lastIndex = 0, srcOffset = 17, srcIndex = srcOffset; trgIndex < _mainKey.Length; trgIndex++)
                {
                    byte swap = _mainKey[trgIndex];
                    swpIndex = (swap + lastIndex + key[srcIndex++]) & 0xff;
                    if (srcIndex >= key.Length)
                        srcIndex = srcOffset;
                    _mainKey[trgIndex] = _mainKey[swpIndex];
                    _mainKey[swpIndex] = swap;
                    lastIndex = swpIndex;
                }
            }
            catch (NullFileChunkException e)
            {
                throw new DecryptoException("Key chunk is corrupted.", _input.FullName, e);
            }

            try
            {
                // Read metadata
                byte[] metaChunk = ReadIndexedChunk(0x63);
                int skipCount = 22;
                for (int i = 0; i < metaChunk.LongLength; i++)
                {
                    if (metaChunk[i] == 58)
                    {
                        skipCount = i + 1;
                        break;
                    }
                }

                // Resolve metadata
                _metadata = JsonConvert.DeserializeObject<Metadata>(
                    Encoding.UTF8.GetString(
                        Convert.FromBase64String(
                            Encoding.ASCII.GetString(
                                metaChunk.Skip(skipCount).ToArray()))
                        .AesEcbDecrypt(_jsonKey).Skip(6).ToArray()));
                if (_metadata?.Title == null)
                    _metadata = _metadata = JsonConvert.DeserializeObject<RadioMetadata>(
                        Encoding.UTF8.GetString(
                            Convert.FromBase64String(
                                Encoding.ASCII.GetString(
                                    metaChunk.Skip(skipCount).ToArray()))
                            .AesEcbDecrypt(_jsonKey).Skip(6).ToArray())).MainMusic;
            }
            catch (NullFileChunkException)
            {
                Logger.Log("File does not contain metadata.", _input.FullName, LogLevel.Warn);
            }
            catch
            {
                Logger.Log("Metadata seems corrupted.", _input.FullName, LogLevel.Warn);
            }

            // Skip ahead
            _ = _buffer.Seek(9, SeekOrigin.Current);

            // Get cover data
            try
            {
                // Plan A: Read cover from file
                _coverBuffer = ReadIndexedChunk(null);
            }
            catch (NullFileChunkException)
            {
                Logger.Log("File does not contain cover image. Trying to get from server...", _input.FullName, LogLevel.Warn);

                // Plan B: get image from server
                try
                {
                    string coverUri = _metadata?.AlbumPic;
                    if (!Uri.IsWellFormedUriString(coverUri, UriKind.Absolute))
                    {
                        Logger.Log("File does not contain cover link.", _input.FullName, LogLevel.Error);
                        throw;
                    }
                    using var webClient = new WebClient();
                    _coverBuffer = webClient.DownloadData(coverUri);
                }
                catch
                {
                    Logger.Log("Failed to download cover image.", _input.FullName, LogLevel.Fatal);
                }
            }
            _coverType = _coverBuffer.ParseImageType();
            if (_coverType == null)
            {
                _coverBuffer = null;
            }

            // Read music
            _buffer.Origin = _buffer.Position;
            _buffer.PerformEach((x, i) =>
            {
                int j = (i + 1) & 0xff;
                return (byte)(x ^ (_mainKey[(_mainKey[j] + _mainKey[(_mainKey[j] + j) & 0xff]) & 0xff]));
            });
            _musicType = _buffer.ToArray().ParseMusicType();
        }

        protected override void PostDecrypt()
        {
            base.PostDecrypt();

            _buffer.ResetPosition();
            using TagLib.File file = TagLib.File.Create(_buffer);
            TagLib.Tag tag = _musicType switch
            {
                MusicTypes.Flac => file.Tag,
                MusicTypes.Mpeg => file.GetTag(TagLib.TagTypes.Id3v2),
                _ => throw new DecryptoException("Media stream seems corrupted.", _input.FullName),
            };

            if (_coverType != null)
            {
                tag.Pictures = new TagLib.IPicture[1] {
                    new TagLib.Picture(new TagLib.ByteVector(_coverBuffer))
                    {
                        MimeType = _coverType?.GetMime(),
                        Type = TagLib.PictureType.FrontCover
                    }
                };
            }

            if (_metadata?.Title != null)
                tag.Title = _metadata?.Title;
            if (_metadata?.Artists?.Count() > 0)
            {
                tag.Performers = _metadata?.Artists?.ToArray();
                tag.AlbumArtists = new string[] { _metadata?.Artists?.First() };
            }
            if (_metadata?.Album != null)
                tag.Album = _metadata?.Album;

            file.Save();
        }

        private byte[] ReadIndexedChunk(byte? obfuscator)
        {
            int chunkSize = _reader.ReadInt32();

            if (chunkSize > 0)
            {
                var chunk = new byte[chunkSize];
                _buffer.Read(chunk, 0, chunkSize);
                if (obfuscator != null)
                {
                    for (int i = 0; i < chunkSize; i++)
                        chunk[i] ^= obfuscator.Value;
                }
                return chunk;
            }
            else
            {
                throw new NullFileChunkException("Failed to load file chunk.");
            }
        }

        private struct Metadata
        {
            public string Title { get; set; }
            public IList<IList<string>> Artist { private get; set; }
            public string Album { get; set; }
            public string AlbumPic { get; set; }

            public IEnumerable<string> Artists => Artist?.Select(tuple => tuple[0]);
        }

        private struct RadioMetadata
        {
            public Metadata MainMusic { get; set; }
        }
    }
}
