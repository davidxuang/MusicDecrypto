using System;
using System.IO;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using TagLib;

namespace MusicDecrypto.Library;

public abstract class DecryptoBase : IDisposable
{
    protected MarshalMemoryStream _buffer;
    protected BinaryReader _reader;
    protected AudioTypes _audioType;
    protected string _oldName;
    protected string? _newBaseName;
    protected long _startOffset = 0;
    protected bool _decrypted;

    protected DecryptoBase(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type = AudioTypes.Undefined)
    {
        _buffer = buffer;
        _reader = new(buffer);
        _oldName = name;
        if (warn != null) Warn += warn;
        _audioType = type;
        _buffer.ResetPosition();
    }

    protected abstract IDecryptor Decryptor { get; init; }

    public void Dispose()
    {
        _reader?.Dispose();
        _buffer.Dispose();
        if (Decryptor is IDisposable decryptor) { decryptor.Dispose(); }
        GC.SuppressFinalize(this);
    }

    public Info Decrypt()
    {
        if (!_decrypted)
        {
            _buffer.ResetPosition();
            var offset = _startOffset;
            while (offset < _buffer.Length)
            {
                var block = _buffer.AsPaddedSpan(offset);
                offset += Decryptor.Decrypt(block, offset);
            }
            _decrypted = true;
            return GetMetadata();
        }
        return GetMetadata(false);
    }

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
                var modified = ProcessMetadataOverride(tag);
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
                (_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)) + _audioType.GetExtension(),
                tag.Title,
                tag.Performers.Length > 0 ? tag.Performers[0] : null,
                tag.Album,
                tag.Pictures.Length > 0 ? tag.Pictures[0].Data.Data : null);
        }
        else RaiseWarn("Reading tags from DFF files is not supported.");

        return new Info((_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)) + _audioType.GetExtension());
    }
    protected virtual bool ProcessMetadataOverride(Tag tag) { return false; } // return whether metadata is modified

    public delegate void WarnHandler(string message);
    public event WarnHandler? Warn;
    public void RaiseWarn(string message) => Warn?.Invoke(message);

    public record class Info(
        string NewName,
        string? Title = null,
        string? Artist = null,
        string? Album = null,
        byte[]? Cover = null);
}
