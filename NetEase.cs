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
        private byte[] MainKey { get; } = new byte[256];
        private NetEaseMetadata PropMetadata { get; set; }

        internal NetEaseDecrypto(string path) : base(path) { Load(); FixMetadata(); Save(); }

        protected override void Load()
        {
            // Check file header
            if (SrcFile.ReadUInt64() != 0x4d4144464e455443)
                throw new FileLoadException($"Failed to recognize header in {SrcPath}.");

            // Skip ahead
            _ = SrcFile.ReadBytes(2);

            try
            {
                // Read key
                byte[] keyChunk = ReadIndexedChunk(0x64);
                byte[] key = AesCrypto.EcbDecrypt(keyChunk, rootKey);

                // Build main key
                for (uint i = 0; i < MainKey.Length; i += 1)
                {
                    MainKey[i] = Convert.ToByte(i);
                }
                for (uint trgIndex = 0, swpIndex, lastIndex = 0, srcOffset = 17, srcIndex = srcOffset; trgIndex < MainKey.Length; trgIndex += 1)
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
                throw new FileLoadException(e.ToString());
            }

            try
            {
                // Read metadata
                byte[] metaChunk = ReadIndexedChunk(0x63);
                int skipCount = 22;
                for (int i = 0; i < metaChunk.LongLength; i += 1)
                {
                    if (metaChunk[i] == 58)
                    {
                        skipCount = i + 1;
                        break;
                    }
                }
                string jsonMeta = Encoding.UTF8.GetString(AesCrypto.EcbDecrypt(
                    Convert.FromBase64String(Encoding.UTF8.GetString(metaChunk.Skip(skipCount).ToArray())), jsonKey).Skip(6).ToArray());

                // Resolve metadata
                PropMetadata = JsonConvert.DeserializeObject<NetEaseMetadata>(jsonMeta);
                StdMetadata = new Metadata(PropMetadata);
            }
            catch (NullFileChunkException)
            {
                Console.WriteLine($"[Warning] Missing metadata in {SrcPath}.");
            }

            // Skip ahead
            _ = SrcFile.ReadBytes(9);

            // Get cover data
            try
            {
                // Plan A: Read cover from file
                CoverBuffer.Write(ReadIndexedChunk(null));
            }
            catch (NullFileChunkException)
            {
                Console.WriteLine($"[Warning] Missing cover image in {SrcPath}.");

                // Plan B: get image from server
                try
                {
                    string coverUri = PropMetadata.AlbumPic;
                    if (!Uri.IsWellFormedUriString(coverUri, UriKind.Absolute))
                    {
                        Console.WriteLine($"[Warning] No cover URI defined in {SrcPath}.");
                        throw;
                    }
                    using WebClient webClient = new WebClient();
                    CoverBuffer.Write(webClient.DownloadData(coverUri));
                }
                finally
                {
                    Console.WriteLine($"[Warning] Failed to download cover image for {SrcPath}.");
                }
            }
            CoverMime = MediaType.GetStreamMime(CoverBuffer);

            // Read music
            for (int chunkSize = 0x8000; chunkSize > 1;)
            {
                byte[] musicChunk = ReadFixedChunk(ref chunkSize);

                for (int i = 0; i < chunkSize; i += 1)
                {
                    int j = (i + 1) & 0xff;
                    musicChunk[i] ^= MainKey[(MainKey[j] + MainKey[(MainKey[j] + j) & 0xff]) & 0xff];
                }

                Buffer.Write(musicChunk);
            }
            MusicMime = MediaType.GetStreamMime(Buffer);
        }

        protected override void FixMetadata()
        {
            Buffer.Position = 0;
            using TagLib.File file = TagLib.File.Create(new MemoryFileAbstraction($"buffer.{MediaType.MimeToExt(MusicMime)}", Buffer));
            TagLib.Tag tag = MusicMime switch
            {
                "audio/flac" => file.Tag,
                "audio/mpeg" => file.GetTag(TagLib.TagTypes.Id3v2),
                _ => throw new FileLoadException($"Failed to get file type while processing {SrcPath}."),
            };

            if (CoverBuffer.Length > 0)
            {
                TagLib.Picture picture;
                TagLib.ByteVector vector = new TagLib.ByteVector(CoverBuffer.ToArray(), (int)CoverBuffer.Length);
                picture = new TagLib.Picture(vector)
                {
                    MimeType = CoverMime,
                    Type = TagLib.PictureType.FrontCover
                };
                TagLib.IPicture[] pictures = new TagLib.IPicture[1] { picture };
                tag.Pictures = pictures;
            }

            if (StdMetadata.Title != null)
                tag.Title = StdMetadata.Title;
            if (StdMetadata.Artist != null)
                tag.Performers = StdMetadata.Artist;
            if (StdMetadata.Album != null)
                tag.Album = StdMetadata.Album;

            file.Save();
        }
    }
}
