using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using MusicDecrypto.Library.Cryptography;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Numerics;
using TagLib;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed partial class QmcDecrypto : DecryptoBase
{
    private static readonly byte[] _v2Magic = "QQMusic EncV2,Key:"u8.ToArray();
    private static readonly byte[] _v2TeaKey1 = "386ZJY!@#*$%^&)("u8.ToArray();
    private static readonly byte[] _v2TeaKey2 = "**#!(#$%&^a1cZ,T"u8.ToArray();
    private static readonly Regex _regex = new("^[0-9A-F]{16,}$");

    protected override IDecryptor Decryptor { get; init; }

    public QmcDecrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type) : base(buffer, name, warn, type)
    {
        _ = _buffer.Seek(-4, SeekOrigin.End);
        int indicator = _reader.ReadInt32();

        byte[]? key = null;
        long length;

        switch (indicator) // "QTag"
        {
            case 0x67615451:
                {
                    int chunkLength = BinaryPrimitives.ReadInt32BigEndian(_reader.ReadBytes(4));
                    length = _buffer.Length - 8 - chunkLength;
                    var metas = Encoding.ASCII.GetString(_buffer.AsSpan((int)length, chunkLength)).Split(',');
                    key = DecryptKey(metas[0]);
                    // var id = ulong.Parse(metas[1]);
                    break;
                }

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
    protected override bool ProcessMetadataOverride(Tag tag)
    {
        if (tag == null) return false;

        var baseName = Path.GetFileNameWithoutExtension(_oldName);

        if (_regex.IsMatch(baseName))
        {
            if (tag.Title != null && tag.AlbumArtists.Length > 0)
            {
                _newBaseName = string.Join(" - ", tag.AlbumArtists[0], tag.Title);
                RaiseWarn($"New filename “{_newBaseName}”");
            }
            else if (tag.Title != null && tag.Performers.Length > 0)
            {
                _newBaseName = string.Join(" - ", tag.Performers[0], tag.Title);
                RaiseWarn($"New filename “{_newBaseName}”");
            }
            else RaiseWarn("Detected hashed filename but failed to determine new name.");
        }

        if (tag.Pictures.Length > 0)
        {
            if (tag.Pictures[0].Type != PictureType.FrontCover)
            {
                tag.Pictures[0].Type = PictureType.FrontCover;
                return true;
            }
        }

        return false;
    }

    public static byte[] DecryptKey(string value)
    {
        var key = Convert.FromBase64String(value.TrimEnd('\0')).AsSpan();
        int length = key.Length;

        if (length < 24)
            throw new InvalidDataException($"Key length should be 24 at least. (got {length})");

        if (key[..18].SequenceEqual(_v2Magic))
        {
            key = key[18..];

            DecryptTeaCbc(_v2TeaKey1, key, out length);
            DecryptTeaCbc(_v2TeaKey2, key[..length], out length);

            key = Convert.FromBase64String(Encoding.ASCII.GetString(key[..length])).AsSpan();
            length = key.Length;

            if (length < 8)
                throw new InvalidDataException($"Key length should be 8 at least. (got {length})");
        }

        if (length % 8 != 0)
            throw new InvalidDataException($"Key length should be a multiple of 8. (got {length})");

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
        if (length % 8 != 0)
            throw new InvalidDataException($"Cipher text length should be a multiple of 8. (got {length})");

        var raw = (stackalloc byte[SimdHelper.GetPaddedLength(length)]);
        var res = (stackalloc byte[SimdHelper.GetPaddedLength(length)]);
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
            if (b != 0x00)
                throw new InvalidDataException("Zero check failed.");
        }

        var data = res[(1 + padLength + saltLength)..(length - zeroLength)];
        data.CopyTo(buffer);

        length = data.Length;
    }
}
