using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
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

    protected DecryptoBase(
        MarshalMemoryStream buffer,
        string name,
        WarnHandler? warn,
        MatchRequestHandler? matchConfirm = null,
        AudioTypes type = AudioTypes.Undefined)
    {
        _buffer = buffer;
        _reader = new(buffer);
        _oldName = name;
        if (warn is not null) Warn += warn;
        if (matchConfirm is not null) OnRequestMatch += matchConfirm;
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

    public async ValueTask<Info> DecryptAsync()
    {
        if (!_decrypted)
        {
            _buffer.ResetPosition();
            var offset = _startOffset;
            while (offset < _buffer.Length)
            {
                offset += Decryptor.Decrypt(_buffer.AsPaddedSpan(offset), offset);
            }
            _decrypted = true;
            return await GetMetadataAsync();
        }
        return await GetMetadataAsync(false);
    }

    private async ValueTask<Info> GetMetadataAsync(bool firstRun = true)
    {
        if (_audioType == AudioTypes.Undefined) _audioType = ((ReadOnlySpan<byte>)_buffer.AsSpan()).SniffAudioType();
        _buffer.Name = "buffer" + _audioType.GetExtension();
        _buffer.ResetPosition();

        if (_audioType != AudioTypes.XDff)
        {
            using TagLib.File file = TagLib.File.Create(_buffer);
            Tag tag = _audioType == AudioTypes.Mpeg ? file.GetTag(TagTypes.Id3v2) : file.Tag;

            if (firstRun)
            {
                var modified = await ProcessMetadataOverrideAsync(tag);
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
                        OnWarn("Detected and converted non-standard ID3 tags.");
                    }

                    if (file.GetTag(TagTypes.Xiph) is TagLib.Ogg.XiphComment meta)
                    {
                        var mqa = meta.GetField("MQAENCODER");
                        if (mqa.Length > 0) OnWarn("Detected MQA-encoded FLAC stream.");
                    }
                }

                if (modified) file.Save();
            }

            return new Info(
                (_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)) + _audioType.GetExtension(),
                tag.Title,
                string.Join("; ", tag.Performers),
                tag.Album,
                tag.Pictures.Length > 0 ? tag.Pictures[0].Data.Data : null);
        }
        else OnWarn("Reading tags from DFF files is not supported.");

        return new Info((_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)) + _audioType.GetExtension());
    }
    protected virtual ValueTask<bool> ProcessMetadataOverrideAsync(Tag tag)
        => ValueTask.FromResult(false); // return whether metadata is modified

    public delegate void WarnHandler(string message);
    private event WarnHandler? Warn;
    protected void OnWarn(string message) => Warn?.Invoke(message);

    public delegate ValueTask<bool> MatchRequestHandler(string message, IEnumerable<MatchInfo> properties);
    private readonly MatchRequestHandler? OnRequestMatch;
    protected async ValueTask<bool> RequestMatchAsync((string, string, string) local, (string, string, string) online)
        => OnRequestMatch is not null && await OnRequestMatch.Invoke(
            "Metadata matching confirmation",
            ImmutableArray.Create<MatchInfo>(
                new("Local",  local.Item1,  local.Item2,  local.Item3),
                new("Online", online.Item1, online.Item2, online.Item3)));

    public record class Info(
        string NewName,
        string? Title = null,
        string? Performers = null,
        string? Album = null,
        byte[]? Cover = null);

    public record class MatchInfo(
        string Key,
        string Title,
        string Performers,
        string Album);
}
