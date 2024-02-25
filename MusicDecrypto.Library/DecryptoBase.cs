using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using TagLib;

namespace MusicDecrypto.Library;

public abstract class DecryptoBase : IDisposable
{
    public delegate ValueTask<TResult> RequestHandler<TPayload, TResult>(DecryptoBase sender, string? message, TPayload payload);
    // public static event RequestHandler<Vendors, string?>? OnRequestKey;
    public static event RequestHandler<ValueTuple<MatchInfo, MatchInfo>, bool>? OnRequestMatch;

    public delegate void WarnHandler(string message);
    private event WarnHandler? Warn;

    protected MarshalMemoryStream _buffer;
    protected BinaryReader _reader;
    protected AudioType _audioType;
    protected string _oldName;
    protected string? _newBaseName;
    protected long _startOffset = 0;
    protected bool _decrypted;

    protected DecryptoBase(
        MarshalMemoryStream buffer,
        string name,
        WarnHandler? warn,
        AudioType type = AudioType.Undefined)
    {
        _buffer = buffer;
        _reader = new(buffer);
        _oldName = name;
        if (warn is not null) Warn += warn;
        _audioType = type;
        _buffer.ResetPosition();
    }

    protected abstract IDecryptor Decryptor { get; init; }

    public void Dispose()
    {
        _reader?.Dispose();
        _buffer.Dispose();
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
            if (Decryptor.TrimStart != 0)
            {
                _buffer.Origin += Decryptor.TrimStart;
            }
            _decrypted = true;
            return await GetMetadataAsync();
        }
        return await GetMetadataAsync(false);
    }

    private async ValueTask<Info> GetMetadataAsync(bool firstRun = true)
    {
        if (_audioType == AudioType.Undefined) _audioType = _buffer.AsSpan().SniffAudioType();
        _buffer.Name = "buffer" + _audioType.GetExtension();
        _buffer.ResetPosition();
        _newBaseName ??= Path.GetFileNameWithoutExtension(_oldName)!;

        if (_audioType == AudioType.XDff)
        {
            RaiseWarn("Reading tags from DFF files is not supported.");
            return new Info($"{_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)}{_audioType.GetExtension()}");
        }
        else
        {
            using TagLib.File file = TagLib.File.Create(_buffer);
            Tag tag = _audioType == AudioType.Mpeg ? file.GetTag(TagTypes.Id3v2) : file.Tag;

            if (firstRun)
            {
                var modified = await ProcessMetadataOverrideAsync(tag);
                if (_audioType == AudioType.Flac)
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
                $"{_newBaseName ?? Path.GetFileNameWithoutExtension(_oldName)}{_audioType.GetExtension()}",
                tag.Title,
                string.Join("; ", tag.Performers),
                tag.Album,
                tag.Pictures.Length > 0 ? tag.Pictures[0].Data.Data : null);
        }
    }
    protected virtual ValueTask<bool> ProcessMetadataOverrideAsync(TagLib.Tag tag)
        => ValueTask.FromResult(false); // return whether metadata is modified

    protected void RaiseWarn(string message) => Warn?.Invoke(message);

    //protected async ValueTask<string?> RequestKeyAsync(string message, Vendors vendor)
    //{
    //    return OnRequestKey is null ? null : await OnRequestKey.Invoke(this, message, vendor);
    //}

    protected async ValueTask<bool> RequestMatchAsync(MatchInfo local, MatchInfo online)
    {
        if (OnRequestMatch is null)
        {
            RaiseWarn("No metadata matching callback registered, turning down automatically.");
            return false;
        }
        else
        {
            return await OnRequestMatch.Invoke(this, null, (local, online));
        }
    }

    public record class Info(
        string NewName,
        string? Title = null,
        string? Performers = null,
        string? Album = null,
        byte[]? Cover = null);

    public record class MatchInfo(
        string Title,
        string Performers,
        string Album);
}
