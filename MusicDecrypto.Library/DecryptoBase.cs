using System;
using System.Collections.Generic;
using System.IO;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using MusicDecrypto.Library.Vendor;
using TagLib;

namespace MusicDecrypto.Library
{
    public abstract class DecryptoBase : IDisposable
    {
        public enum DecryptoTypes : byte
        {
            NetEase = 0x11,
            TencentBasic = 0x21,
            TencentKey = 0x22,
            KugouBasic = 0x31,
            KugouViper = 0x32,
            Kuwo = 0x41,
            Xiami = 0x51,
        }

        public static Dictionary<string, ValueTuple<DecryptoTypes, AudioTypes>> KnownExtensions => new()
        {
            { ".ncm", (DecryptoTypes.NetEase, AudioTypes.Undefined) },
            { ".tm2", (DecryptoTypes.TencentBasic, AudioTypes.Mp4) },
            { ".tm6", (DecryptoTypes.TencentBasic, AudioTypes.Mp4) },
            { ".qmc0", (DecryptoTypes.TencentKey, AudioTypes.Mpeg) },
            { ".qmc3", (DecryptoTypes.TencentKey, AudioTypes.Mpeg) },
            { ".qmc2", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".qmc4", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".qmc6", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".qmc8", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".tkm", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".qmcogg", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".qmcflac", (DecryptoTypes.TencentKey, AudioTypes.Flac) },
            { ".bkcmp3", (DecryptoTypes.TencentKey, AudioTypes.Mpeg) },
            { ".bkcm4a", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".bkcwma", (DecryptoTypes.TencentKey, AudioTypes.XWma) },
            { ".bkcogg", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".bkcwav", (DecryptoTypes.TencentKey, AudioTypes.XWav) },
            { ".bkcape", (DecryptoTypes.TencentKey, AudioTypes.XApe) },
            { ".bkcflac", (DecryptoTypes.TencentKey, AudioTypes.Flac) },
            { ".mgg", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".mgg1", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".mggl", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".mflac", (DecryptoTypes.TencentKey, AudioTypes.Flac) },
            { ".mflac0", (DecryptoTypes.TencentKey, AudioTypes.Flac) },
            { ".6d7033", (DecryptoTypes.TencentKey, AudioTypes.Mpeg) },
            { ".6d3461", (DecryptoTypes.TencentKey, AudioTypes.Mp4) },
            { ".6f6767", (DecryptoTypes.TencentKey, AudioTypes.Ogg) },
            { ".776176", (DecryptoTypes.TencentKey, AudioTypes.XWav) },
            { ".666c6163", (DecryptoTypes.TencentKey, AudioTypes.Flac) },
            { ".kgm", (DecryptoTypes.KugouBasic, AudioTypes.Undefined) },
            { ".kgma", (DecryptoTypes.KugouBasic, AudioTypes.Undefined) },
            { ".vpr", (DecryptoTypes.KugouViper, AudioTypes.Undefined) },
            { ".kwm", (DecryptoTypes.Kuwo, AudioTypes.Undefined) },
            { ".xm", (DecryptoTypes.Xiami, AudioTypes.Undefined) },
            { ".mp3", (DecryptoTypes.Xiami, AudioTypes.Mpeg) },
            { ".m4a", (DecryptoTypes.Xiami, AudioTypes.Mp4) },
            { ".wav", (DecryptoTypes.Xiami, AudioTypes.XWav) },
            { ".flac", (DecryptoTypes.Xiami, AudioTypes.Flac) },
        };

        protected MarshalMemoryStream _buffer;
        protected AudioTypes _audioType;
        protected bool _decrypted;
        protected string? _newBaseName;

        protected BinaryReader? _reader;
        protected BinaryReader Reader
        {
            get
            {
                if (_reader == null) _reader = new(_buffer);
                return _reader;
            }
        }

        public string Name { get; protected set; }

        protected DecryptoBase(MarshalMemoryStream buffer, string name, AudioTypes type = AudioTypes.Undefined)
        {
            _buffer = buffer;
            Name = name;
            _audioType = type;
        }

        public static DecryptoBase Create(MarshalMemoryStream buffer, string name)
        {
            (var cipher, var format) = KnownExtensions[Path.GetExtension(name)];
            return cipher switch
            {
                DecryptoTypes.NetEase => new NetEase(buffer, name),
                DecryptoTypes.TencentBasic => new TencentBasic(buffer, name, format),
                DecryptoTypes.TencentKey => new TencentKey(buffer, name, format),
                DecryptoTypes.KugouBasic => new KugouBasic(buffer, name),
                DecryptoTypes.KugouViper => new KugouViper(buffer, name),
                DecryptoTypes.Kuwo => new Kuwo(buffer, name),
                DecryptoTypes.Xiami => new Xiami(buffer, name, format),
                _ => throw new NotSupportedException("Unknown extension."),
            };
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _buffer.Dispose();
            GC.SuppressFinalize(this);
        }

        public Info Decrypt()
        {
            if (!_decrypted)
            {
                _buffer.ResetPosition();
                Process();
                _decrypted = true;
                return GetMetadata();
            }
            return GetMetadata(false);
        }

        protected abstract void Process();

        private Info GetMetadata(bool firstRun = true)
        {
            if (_audioType == AudioTypes.Undefined) _audioType = _buffer.SniffAudioType();
            _buffer.Name = "buffer" + _audioType.GetExtension();
            _buffer.ResetPosition();

            if (_audioType != AudioTypes.XDff)
            {
                using TagLib.File file = TagLib.File.Create(_buffer);
                Tag tag = _audioType == AudioTypes.Mpeg ? file.GetTag(TagTypes.Id3v2) : file.Tag;

                if (firstRun)
                {
                    var modified = MetadataMisc(tag);
                    if (_audioType == AudioTypes.Flac)
                    {
                        if (file.TagTypes.HasFlag(TagTypes.Id3v1) || file.TagTypes.HasFlag(TagTypes.Id3v2))
                        {
                            if (file.TagTypes.HasFlag(TagTypes.Id3v2))
                                file.GetTag(TagTypes.Id3v2).CopyTo(file.GetTag(TagTypes.FlacMetadata), false);
                            else if (file.TagTypes.HasFlag(TagTypes.Id3v1))
                                file.GetTag(TagTypes.Id3v1).CopyTo(file.GetTag(TagTypes.FlacMetadata), false);
                            file.RemoveTags(TagTypes.Id3v1 | TagTypes.Id3v2);
                            modified = true;
                            RaiseWarn("Detected and converted non-standard ID3 tags.");
                        }

                        if (file.GetTag(TagTypes.Xiph) is TagLib.Ogg.XiphComment meta)
                        {
                            var mqa = meta.GetField("MQAENCODER");
                            if (mqa.Length > 0) RaiseWarn("Detected MQA-encoded FLAC stream.");
                        }
                    }

                    if (modified) file.Save();
                }

                return new Info(
                    (_newBaseName ?? Path.GetFileNameWithoutExtension(Name)) + _audioType.GetExtension(),
                    tag.Title,
                    tag.Performers.Length > 0 ? tag.Performers[0] : null,
                    tag.Album,
                    tag.Pictures.Length > 0 ? tag.Pictures[0].Data.Data : null);
            }
            else RaiseWarn("Reading tags from DFF files is not supported.");

            return new Info((_newBaseName ?? Path.GetFileNameWithoutExtension(Name)) + _audioType.GetExtension());
        }
        protected virtual bool MetadataMisc(Tag tag) { return false; } // return true when needs modify

        public record class Info(
            string NewName,
            string? Title = null,
            string? Artist = null,
            string? Album = null,
            byte[]? Cover = null);

        public delegate void WarnHandler(string message);
        public event WarnHandler? Warn;
        public void RaiseWarn(string message) => Warn?.Invoke(message);
    }
}
