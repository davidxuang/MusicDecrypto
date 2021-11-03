using MusicDecrypto.Library.Common;
using System;
using System.IO;
using System.Linq;

namespace MusicDecrypto.Library.Vendor
{
    public abstract class TencentDecrypto : Decrypto
    {
        public static bool RenewName { get; set; }

        protected TencentDecrypto(FileInfo file, AudioTypes type) : base(file, type) { }

        protected override void PostDecrypt()
        {
            base.PostDecrypt();

            _buffer.ResetPosition();
            using TagLib.File file = TagLib.File.Create(_buffer);
            TagLib.Tag tag = _musicType switch
            {
                AudioTypes.Flac => file.Tag,
                AudioTypes.Ogg  => file.Tag,
                AudioTypes.Mp4  => file.Tag,
                AudioTypes.Mpeg => file.GetTag(TagLib.TagTypes.Id3v2),
                _ => throw new DecryptoException("File has an unexpected MIME value.", _input.FullName),
            };
            if (tag == null) return;

            if (tag.Pictures.Length > 0)
                tag.Pictures[0].Type = TagLib.PictureType.FrontCover;

            if (RenewName)
            {
                if (tag.Title != null && tag.AlbumArtists.Length > 0)
                    _outName = tag.AlbumArtists[0] + " - " + tag.Title;
                else if (tag.Title != null && tag.Performers.Length > 0)
                    _outName = tag.Performers[0] + " - " + tag.Title;
                else
                    Logger.Log("Failed to find name.", _input.FullName, LogLevel.Error);
            }

            file.Save();
        }
    }

    public sealed class TencentSimpleDecrypto : TencentDecrypto
    {
        private static readonly byte[] _header = { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 };

        public TencentSimpleDecrypto(FileInfo file, AudioTypes type) : base(file, type) { }

        protected override void Decrypt()
        {
            _buffer.Write(_header);
        }
    }

    public abstract class TencentMaskDecrypto : TencentDecrypto
    {
        protected int _length;

        protected TencentMaskDecrypto(FileInfo file, AudioTypes type) : base(file, type)
        {
            _length = Convert.ToInt32(_buffer.Length);
        }

        protected override void Decrypt()
        {
            _buffer.SetLength(_length);
            _buffer.PerformEach((x, i) => (byte)(x ^ GetMask(i)));
        }

        protected byte GetMask(int index)
            => Mask((index < 0x8000 ? index : index + (index / 0x7FFF)) % 0x80);

        protected abstract byte Mask(int offset);
    }

    public sealed class TencentStaticDecrypto : TencentMaskDecrypto
    {
        private static readonly byte[] _mask = new byte[]
            {
                0xc3, 0x4a, 0xd6, 0xca, 0x90, 0x67, 0xf7, 0x52,
                0xd8, 0xa1, 0x66, 0x62, 0x9f, 0x5b, 0x09, 0x00,
                0xc3, 0x5e, 0x95, 0x23, 0x9f, 0x13, 0x11, 0x7e,
                0xd8, 0x92, 0x3f, 0xbc, 0x90, 0xbb, 0x74, 0x0e,
                0xc3, 0x47, 0x74, 0x3d, 0x90, 0xaa, 0x3f, 0x51,
                0xd8, 0xf4, 0x11, 0x84, 0x9f, 0xde, 0x95, 0x1d,
                0xc3, 0xc6, 0x09, 0xd5, 0x9f, 0xfa, 0x66, 0xf9,
                0xd8, 0xf0, 0xf7, 0xa0, 0x90, 0xa1, 0xd6, 0xf3,
                0xc3, 0xf3, 0xd6, 0xa1, 0x90, 0xa0, 0xf7, 0xf0,
                0xd8, 0xf9, 0x66, 0xfa, 0x9f, 0xd5, 0x09, 0xc6,
                0xc3, 0x1d, 0x95, 0xde, 0x9f, 0x84, 0x11, 0xf4,
                0xd8, 0x51, 0x3f, 0xaa, 0x90, 0x3d, 0x74, 0x47,
                0xc3, 0x0e, 0x74, 0xbb, 0x90, 0xbc, 0x3f, 0x92,
                0xd8, 0x7e, 0x11, 0x13, 0x9f, 0x23, 0x95, 0x5e,
                0xc3, 0x00, 0x09, 0x5b, 0x9f, 0x62, 0x66, 0xa1,
                0xd8, 0x52, 0xf7, 0x67, 0x90, 0xca, 0xd6, 0x4a,
            };

        public TencentStaticDecrypto(FileInfo file, AudioTypes type) : base(file, type) { }

        protected override byte Mask(int index) => _mask[index];
    }

    public sealed class TencentDynamicDecrypto : TencentMaskDecrypto
    {
        private byte[] _mask;

        public TencentDynamicDecrypto(FileInfo file, AudioTypes type) : base(file, type) { }

        protected override void PreDecrypt()
        {
            int headerSize = 0x8;
            byte[] header = new byte[headerSize];
            if (_reader.Read(header) < headerSize)
                throw new DecryptoException("File seems incomplete.", _input.FullName);
            _buffer.ResetPosition();
            int maskSize = 0x80;
            for (int i = 0; i < Math.Min(0x100, _length / maskSize); i++)
            {
                byte[] candidate = _reader.ReadBytes(maskSize);
                if (header.Select((x, i) => (byte)(x ^ candidate[i])).SniffAudioType() == AudioTypes.Flac)
                {
                    _mask = candidate;
                    break;
                }
            }
            if (_mask == null)
                throw new DecryptoException($"File is currently not supported.", _input.FullName);

            _buffer.Seek(-4, SeekOrigin.End);
            uint keyLength = _reader.ReadUInt32();
            _length -= (int)(keyLength + 4);
            _buffer.ResetPosition();
        }

        protected override byte Mask(int index) => _mask[index];
    }
}
