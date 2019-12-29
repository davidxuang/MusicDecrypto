using System;
using System.Linq;
using System.IO;

namespace MusicDecrypto
{
    internal abstract class TencentDecrypto : Decrypto
    {
        public static bool ForceRename { get; set; } = false;

        internal TencentDecrypto(string path, string mime) : base(path, mime) { }

        protected override void Load()
        {
            for (int chunkSize = 0x8000; ;)
            {
                byte[] chunk = ReadFixedChunk(ref chunkSize);

                for (int i = 0; i < chunkSize; i++)
                {
                    chunk[i] ^= NextMask();
                }

                if (chunkSize < 0x8000)
                {
                    OutBuffer.Write(chunk.Take(chunkSize).ToArray());
                    break;
                }
                else
                    OutBuffer.Write(chunk);
            }
        }

        protected override void Metadata()
        {
            ResetOutBuffer();
            using TagLib.File file = TagLib.File.Create(new MemoryFileAbstraction($"buffer.{MediaType.MimeToExt(MusicMime)}", OutBuffer));
            TagLib.Tag tag = MusicMime switch
            {
                "audio/flac" => file.Tag,
                "audio/mpeg" => file.GetTag(TagLib.TagTypes.Id3v2),
                _ => throw new FileLoadException($"Failed to get file type while processing {InPath}."),
            };

            if (tag.Pictures.Length > 0)
            {
                tag.Pictures[0].Type = TagLib.PictureType.FrontCover;
            }

            if (ForceRename)
            {
                if (tag.Title != null && tag.AlbumArtists.Length > 0)
                    OutName = $"{tag.AlbumArtists[0]} - {tag.Title}";
                else if (tag.Title != null && tag.Performers.Length > 0)
                    OutName = $"{tag.Performers[0]} - {tag.Title}";
                else
                    Console.WriteLine($"[WARN] Failed to find name for {InPath}.");
            }

            file.Save();
        }

        protected abstract byte NextMask();
    }

    internal sealed class TencentLegacyDecrypto : TencentDecrypto
    {
        private static readonly byte[,] SeedMap = {
            {0x4a, 0xd6, 0xca, 0x90, 0x67, 0xf7, 0x52},
            {0x5e, 0x95, 0x23, 0x9f, 0x13, 0x11, 0x7e},
            {0x47, 0x74, 0x3d, 0x90, 0xaa, 0x3f, 0x51},
            {0xc6, 0x09, 0xd5, 0x9f, 0xfa, 0x66, 0xf9},
            {0xf3, 0xd6, 0xa1, 0x90, 0xa0, 0xf7, 0xf0},
            {0x1d, 0x95, 0xde, 0x9f, 0x84, 0x11, 0xf4},
            {0x0e, 0x74, 0xbb, 0x90, 0xbc, 0x3f, 0x92},
            {0x00, 0x09, 0x5b, 0x9f, 0x62, 0x66, 0xa1}
        };

        internal TencentLegacyDecrypto(string path, string mime) : base(path, mime) { }

        private int indexX = -1;
        private int indexY = 8;
        private int stepX = 1;
        private int offset = -1;

        protected override byte NextMask()
        {
            byte val;
            offset++;
            if (indexX < 0)
            {
                stepX = 1;
                indexY = (8 - indexY) % 8;
                val = 0xc3;
            }
            else if (indexX > 6)
            {
                stepX = -1;
                indexY = 7 - indexY;
                val = 0xd8;
            }
            else
                val = SeedMap[indexY, indexX];
            indexX += stepX;
            if (offset == 0x8000 || (offset > 0x8000 && (offset + 1) % 0x8000 == 0))
                return NextMask();
            return val;
        }
    }

    internal sealed class TencentNeonDecrypto : TencentDecrypto
    {
        byte[] mask = null;

        internal TencentNeonDecrypto(string path, string mime) : base(path, mime) { }

        protected override void Check()
        {
            int maskSize = 0x80;
            int headerSize = 0x8;
            byte[] lastMask = ReadFixedChunk(ref maskSize);
            byte[] thisMask = ReadFixedChunk(ref maskSize);
            while (maskSize == 0x80)
            {
                bool detected = true;
                for (uint i = 0; i < 0x80; i++)
                {
                    if (lastMask[i] != thisMask[i])
                    {
                        detected = false;
                        break;
                    }
                }
                if (!detected)
                {
                    lastMask = thisMask;
                    thisMask = ReadFixedChunk(ref maskSize);
                    continue;
                }

                ResetInFile();

                byte[] header = ReadFixedChunk(ref headerSize);
                for (uint i = 0; i < headerSize; i++)
                {
                    header[i] ^= thisMask[i];
                }
                MemoryStream headerStream = new MemoryStream(header);
                if (MediaType.GetStreamMime(headerStream) == "audio/flac")
                {
                    mask = new byte[maskSize];
                    mask = thisMask;
                    break;
                }
                headerStream.Dispose();
                lastMask = thisMask;
                thisMask = ReadFixedChunk(ref maskSize);
            }

            if (mask == null)
                throw new FileLoadException($"{InPath} is currently not supported.");

            ResetInFile();
        }

        private int index = -1;
        private int offset = -1;

        protected override byte NextMask()
        {
            offset++;
            index++;

            if (offset == 0x8000 || (offset > 0x8000 && (offset + 1) % 0x8000 == 0))
            {
                offset++;
                index++;
            }
            if (index >= 0x80) index -= 0x80;

            return mask[index];
        }
    }
}
