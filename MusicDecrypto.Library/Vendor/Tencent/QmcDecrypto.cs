using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicDecrypto.Library.Cryptography;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Media.Extensions;
using MusicDecrypto.Library.Numerics;
using TagLib;
using static MusicDecrypto.Library.Vendor.Tencent.ApiClient;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed partial class QmcDecrypto : DecryptoBase
{
    [GeneratedRegex("^[0-9A-F]{16,}$")]
    private static partial Regex HashedFilenameRegex();

    private static readonly byte[] _v2Magic = "QQMusic EncV2,Key:"u8.ToArray();
    private static readonly byte[] _v2TeaKey1 = "386ZJY!@#*$%^&)("u8.ToArray();
    private static readonly byte[] _v2TeaKey2 = "**#!(#$%&^a1cZ,T"u8.ToArray();
    private static readonly Regex _regex = HashedFilenameRegex();

    private readonly ulong _id;

    protected override IDecryptor Decryptor { get; init; }

    public QmcDecrypto(
        MarshalMemoryStream buffer,
        string name,
        WarnHandler? warn,
        AudioType type)
        : base(buffer, name, warn, type)
    {
        _ = _buffer.Seek(-4, SeekOrigin.End);
        int indicator = _reader.ReadInt32();

        byte[]? key = null;
        long length;

        switch (indicator)
        {
            case 0x67615451: // "QTag"
                int chunkLength = BinaryPrimitives.ReadInt32BigEndian(_reader.ReadBytes(4));
                length = _buffer.Length - 8 - chunkLength;
                var metas = Encoding.ASCII.GetString(_buffer.AsSpan((int)length, chunkLength)).Split(',');
                key = DecryptKey(metas[0]);
                _id = ulong.Parse(metas[1]);
                break;

            case 0x67615453: // "STag"
                throw new NotSupportedException("Unsupported new format.");

            case 0x786563: // "musicex "
                _buffer.Seek(-8, SeekOrigin.End);
                var prefix = _reader.ReadInt32();
                if (prefix == 0x6973756d) throw new NotSupportedException("Unsupported new format.");
                else goto default;

            case > 0 and < 0x400:
                length = _buffer.Length - 4 - indicator;
                try
                {
                    key = DecryptKey(Encoding.ASCII.GetString(_buffer.AsSpan((int)length, indicator)));
                }
                catch
                {
                    if (indicator == 0x225) throw new NotSupportedException("Unsupported new format.");
                    else throw;
                }
                break;

            default:
                length = _buffer.Length - 4;
                break;
        }

        _buffer.SetLength(length);

        Decryptor = key is null
                  ? new StaticCipher()
                  : key.Length > 300
                  ? new RC4Cipher(key, 128, 5120)
                  : new MapCipher(key);
    }

    internal static byte[] DecryptKey(string value)
    {
        var key = Convert.FromBase64String(value.TrimEnd('\0')).AsSpan();
        int length = key.Length;

        ThrowInvalidData.IfLessThan(length, 24, "Key length");

        if (key[..18].SequenceEqual(_v2Magic))
        {
            key = key[18..];

            DecryptTeaCbc(_v2TeaKey1, key, out length);
            DecryptTeaCbc(_v2TeaKey2, key[..length], out length);

            key = Convert.FromBase64String(Encoding.ASCII.GetString(key[..length])).AsSpan();
            length = key.Length;

            ThrowInvalidData.IfLessThan(length, 8, "Key length");
        }

        ThrowInvalidData.IfNotEqual(length % 8, 0, "Key length");

        var teaKey = (stackalloc byte[16]);
        for (int i = 0; i < 8; i++)
        {
            teaKey[2 * i] = (byte)(Math.Abs(Math.Tan(106 + i * 0.1)) * 100);
            teaKey[2 * i + 1] = key[i];
        }

        DecryptTeaCbc(teaKey, key[8..], out length);
        return key[..(length + 8)].ToArray();
    }

    private static void DecryptTeaCbc(ReadOnlySpan<byte> teaKey, Span<byte> buffer, out int length)
    {
        const int saltLength = 2;
        const int zeroLength = 7;
        var step = SimdHelper.LaneCount;

        length = buffer.Length;
        ThrowInvalidData.IfNotEqual(length % 8, 0, "Key length");

        var raw = (stackalloc byte[SimdHelper.GetPaddedLength(length)]);
        var res = (stackalloc byte[Math.Max(SimdHelper.GetPaddedLength(length), length + SimdHelper.LaneCount - 8)]);
        buffer.CopyTo(raw);
        buffer.CopyTo(res);

        var tea = new Tea(teaKey, 32);

        tea.DecryptBlock(res[..8]);
        int padLength = res[0] & 0x7;

        var reg = (stackalloc byte[SimdHelper.LaneCount]);

        var pre = res;
        for (int i = 8; i < length; i += 8)
        {
            var cur = res[i..];

            var x = new Vector<byte>(pre);
            var y = new Vector<byte>(cur);
            (x ^ y).CopyTo(reg);
            reg[..8].CopyTo(cur);

            pre = cur;
            tea.DecryptBlock(cur);
        }

        for (int i = 8; i < length; i += step)
        {
            var window = res[i..(i + step)];

            var w = new Vector<byte>(window);
            var s = new Vector<byte>(raw[(i - 8)..(i - 8 + step)]);
            (w ^ s).CopyTo(window);
        }

        foreach (var b in res[(length - zeroLength)..length])
        {
            ThrowInvalidData.IfNotEqual(b, 0, "Zero check result");
        }

        var data = res[(1 + padLength + saltLength)..(length - zeroLength)];
        data.CopyTo(buffer);

        length = data.Length;
    }

    protected override async ValueTask<bool> ProcessMetadataOverrideAsync(Tag tag)
    {
        var modified = false;

        if (tag is null) return modified;

        using var _client = new ApiClient();

        Track? meta = null;
        if (_id != 0)
        {
            try { meta = (await _client.GetTracksInfoAsync(_id))?[0]; }
            catch { RaiseWarn("Failed to retrieve metadata regarding the ID."); }
        }
        else if (!(string.IsNullOrEmpty(tag.Performers[0]) && string.IsNullOrEmpty(tag.Album) || string.IsNullOrEmpty(tag.Title)))
        {
            try
            {
                var results = await _client.SearchAsync(string.Join(' ',
                    tag.Title,
                    string.Join(' ', tag.Performers),
                    tag.Album));

                meta = await FindMatchedTrackAsync(tag, results);
            }
            catch { }
            finally { if (meta == null) RaiseWarn("Failed to match metadata online."); }
        }

        if (meta is not null)
        {
            var album = meta.Album;
            var albumId = album.Id;
            var albumMediaId = string.IsNullOrEmpty(album.Pmid) ? album.Mid : album.Pmid;

            try
            {
                byte[]? coverBuffer = null;
                string[]? performers = null;
                if (!string.IsNullOrEmpty(albumMediaId))
                {
                    var coverTask = _client.GetAlbumCoverByMediaIdAsync(albumMediaId);
                    var performerTask = _client.GetAlbumInfoAsync(albumMediaId);
                    Task.WaitAll(coverTask, performerTask);

                    coverBuffer = coverTask.Result;
                    performers = performerTask.Result?.Singer?.SingerList?.Select(p => p.Name)?.ToArray();
                }
                else if (albumId != 0)
                {
                    coverBuffer = await _client.GetAlbumCoverByIdAsync(albumId);
                }

                if (coverBuffer?.Length > 0)
                {
                    tag.Pictures =
                    [
                        new Picture(new ByteVector(coverBuffer))
                        {
                            MimeType = coverBuffer.AsSpan().SniffImageType().GetMime(),
                            Type = PictureType.FrontCover,
                        }
                    ];
                    modified = true;
                }

                if (performers?.Length > 0)
                {
                    tag.Performers = performers;
                    modified = true;
                }
            }
            catch
            {
                RaiseWarn("Failed to fetch album cover.");
            }
        }

        if (modified == false && tag.Pictures.Length > 0)
        {
            if (tag.Pictures[0].Type != PictureType.FrontCover)
            {
                tag.Pictures[0].Type = PictureType.FrontCover;
                modified = true;
            }
        }

        var baseName = Path.GetFileNameWithoutExtension(_oldName);

        if (_regex.IsMatch(baseName))
        {
            if (!string.IsNullOrEmpty(tag.Title) && tag.AlbumArtists.Length > 0)
            {
                _newBaseName = string.Join(" - ", tag.AlbumArtists[0], tag.Title);
                RaiseWarn($"New filename “{_newBaseName}”");
            }
            else if (!string.IsNullOrEmpty(tag.Title) && tag.Performers.Length > 0)
            {
                _newBaseName = string.Join(" - ", tag.Performers[0], tag.Title);
                RaiseWarn($"New filename “{_newBaseName}”");
            }
            else RaiseWarn("Detected hashed filename but failed to determine new name.");
        }

        return modified;
    }

    private async ValueTask<Track?> FindMatchedTrackAsync(TagLib.Tag tag, IEnumerable<Track> tracks)
    {
        ushort confidence = 100;
        var matchTitles = tracks.Where(t => t.Title == tag.Title);
        if (!matchTitles.Any())
        {
            matchTitles = tracks.Where(t => t.Name == tag.Title);
            confidence -= 40;
            if (!matchTitles.Any()) return null;
        }

        var matchPerformers = matchTitles
            .Where(t => t.Singer
                .Where(p => p.Title == tag.Performers[0] || p.Name == tag.Performers[0])
                .Any());
        if (!matchPerformers.Any()) return null;

        var matchAlbums = matchPerformers.Where(t => t.Album.Title == tag.Album);
        if (!matchAlbums.Any())
        {
            matchAlbums = matchPerformers.Where(t =>
                t.Album.Title.Contains(tag.Album, StringComparison.InvariantCultureIgnoreCase) ||
                tag.Album.Contains(t.Album.Title, StringComparison.InvariantCultureIgnoreCase));
            confidence -= 20;
            if (!matchAlbums.Any()) return null;
        }

        var match = matchAlbums.FirstOrDefault()!;
        if (matchAlbums.Count() > 1 || confidence < 100)
        {
            var confirm = await RequestMatchAsync(
                new(tag.Title,   string.Join("; ", tag.Performers),                    tag.Album        ),
                new(match.Title, string.Join("; ", match.Singer.Select(p => p.Title)), match.Album.Title));

            if (!confirm) return null;
        }

        return match;
    }
}
