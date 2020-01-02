using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace MusicDecrypto
{
    internal sealed class NetEaseDecrypto : Decrypto
    {
        private static readonly byte[] rootKey = { 0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57 };
        private static readonly byte[] jsonKey = { 0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 0x26, 0x30, 0x55, 0x3C, 0x27, 0x28 };
        private byte[] MainKey { get; set; } = new byte[256];
        private BinaryReader InReader { get; set; } = null;
        private NetEaseMetadata PropMetadata { get; set; }

        internal NetEaseDecrypto(string path) : base(path)
        {
            InReader = new BinaryReader(InBuffer);
        }

        public override void Dispose()
        {
            InReader.Dispose();
            base.Dispose();
        }

        protected override void Check()
        {
            // Check file header
            if (InReader.ReadUInt64() != 0x4d4144464e455443)
                throw new FileLoadException($"Failed to verify header of \"{InPath}\".");

            // Skip ahead
            _ = InBuffer.Seek(2, SeekOrigin.Current);
        }

        protected override void Decrypt()
        {
            try
            {
                // Read key
                byte[] keyChunk = ReadIndexedChunk(0x64);
                byte[] key = AesEcbDecrypt(keyChunk, rootKey);

                // Build main key
                for (uint i = 0; i < MainKey.Length; i++)
                {
                    MainKey[i] = Convert.ToByte(i);
                }
                for (uint trgIndex = 0, swpIndex, lastIndex = 0, srcOffset = 17, srcIndex = srcOffset; trgIndex < MainKey.Length; trgIndex++)
                {
                    byte swap = MainKey[trgIndex];
                    swpIndex = (swap + lastIndex + key[srcIndex++]) & 0xff;
                    if (srcIndex >= key.Length)
                        srcIndex = srcOffset;
                    MainKey[trgIndex] = MainKey[swpIndex];
                    MainKey[swpIndex] = swap;
                    lastIndex = swpIndex;
                }
            }
            catch (NullFileChunkException e)
            {
                throw new FileLoadException(e.Message);
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
                PropMetadata = JsonConvert.DeserializeObject<NetEaseMetadata>(
                    Encoding.UTF8.GetString(
                        AesEcbDecrypt(
                            Convert.FromBase64String(
                                Encoding.UTF8.GetString(
                                    metaChunk.Skip(skipCount).ToArray())
                            ),
                            jsonKey)
                        .Skip(6).ToArray())
                );
                StdMetadata = new Metadata(PropMetadata);
            }
            catch (NullFileChunkException)
            {
                Logger.Warn("Missing metadata in {Path}", InPath);
            }

            // Skip ahead
            _ = InBuffer.Seek(9, SeekOrigin.Current);

            // Get cover data
            try
            {
                // Plan A: Read cover from file
                CoverBuffer.Write(ReadIndexedChunk(null));
            }
            catch (NullFileChunkException)
            {
                Logger.Warn("Failed to load cover from {Path}, trying to get from server...", InPath);

                // Plan B: get image from server
                try
                {
                    string coverUri = PropMetadata.AlbumPic;
                    if (!Uri.IsWellFormedUriString(coverUri, UriKind.Absolute))
                    {
                        Logger.Error("No cover URI was found in {Path}", InPath);
                        throw;
                    }
                    using WebClient webClient = new WebClient();
                    CoverBuffer.Write(webClient.DownloadData(coverUri));
                }
                catch (Exception)
                {
                    Logger.Error("Failed to download cover image for {Path}", InPath);
                }
            }
            CoverMime = MediaType.GetStreamMime(CoverBuffer);
            if (CoverMime.Substring(0, 5) != "image")
            {
                CoverBuffer.Dispose();
                CoverMime = null;
            }

            // Read music
            for (int chunkSize = 0x8000; ;)
            {
                byte[] mainChunk = ReadFixedChunk(ref chunkSize);

                for (int i = 0; i < chunkSize; i++)
                {
                    int j = (i + 1) & 0xff;
                    mainChunk[i] ^= MainKey[(MainKey[j] + MainKey[(MainKey[j] + j) & 0xff]) & 0xff];
                }

                if (chunkSize < 0x8000)
                {
                    OutBuffer.Write(mainChunk.Take(chunkSize).ToArray());
                    break;
                }
                else
                    OutBuffer.Write(mainChunk);
            }
            MusicMime = MediaType.GetStreamMime(OutBuffer);
        }

        protected override void Metadata()
        {
            ResetOutBuffer();
            using TagLib.File file = TagLib.File.Create(new MemoryFileAbstraction($"buffer.{MediaType.MimeToExt(MusicMime)}", OutBuffer));
            TagLib.Tag tag = MusicMime switch
            {
                "audio/flac" => file.Tag,
                "audio/mpeg" => file.GetTag(TagLib.TagTypes.Id3v2),
                _ => throw new FileLoadException($"Failed to get file type while processing \"{InPath}\"."),
            };

            if (CoverMime != null)
            {
                tag.Pictures = new TagLib.IPicture[1] {
                    new TagLib.Picture(new TagLib.ByteVector(CoverBuffer.ToArray(), (int)CoverBuffer.Length))
                    {
                        MimeType = CoverMime,
                        Type = TagLib.PictureType.FrontCover
                    }
                };
            }

            if (StdMetadata.Title != null)
                tag.Title = StdMetadata.Title;
            if (StdMetadata.Artists != null)
                tag.Performers = StdMetadata.Artists;
            if (StdMetadata.Album != null)
                tag.Album = StdMetadata.Album;
            if (StdMetadata.AlbumArtist != null && tag.AlbumArtists.Length == 0)
                tag.AlbumArtists = new string[] { StdMetadata.AlbumArtist };

            file.Save();
        }

        private byte[] ReadIndexedChunk(byte? obfuscator)
        {
            int chunkSize = InReader.ReadInt32();

            if (chunkSize > 0)
            {
                byte[] chunk = new byte[chunkSize];
                InBuffer.Read(chunk, 0, chunkSize);
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
    }
}
