using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Numerics;
using static MusicDecrypto.Library.DecryptoBase;

namespace MusicDecrypto.Library.Vendor.Ximalaya;

internal sealed partial class MobileDecrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioType type = AudioType.Undefined)
    : DecryptoBase(buffer, name, warn, type)
{
    private static readonly IDecryptor _decryptor2;
    private static readonly IDecryptor _decryptor3;

    static MobileDecrypto()
    {
        using (var stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("MusicDecrypto.Library.Vendor.Ximalaya.x2m_map.bin")!)
        {
            using var reader2 = new BinaryReader(stream2);
            var map2 = new ushort[(int)(stream2.Length / 2)];
            for (int i = 0; i < map2.Length; i++)
            {
                map2[i] = reader2.ReadUInt16();
            }
            _decryptor2 = new Cipher("xmly"u8, map2);
        }

        using (var stream3 = Assembly.GetExecutingAssembly().GetManifestResourceStream("MusicDecrypto.Library.Vendor.Ximalaya.x3m_map.bin")!)
        {
            using var reader3 = new BinaryReader(stream3);
            var map3 = new ushort[(int)(stream3.Length / 2)];
            for (int i = 0; i < map3.Length; i++)
            {
                map3[i] = reader3.ReadUInt16();
            }
            _decryptor3 = new Cipher("3989d111aad5613940f4fc44b639b292"u8, map3);
        }
    }

    protected override IDecryptor Decryptor { get; init; } = Path.GetExtension(name) switch
    {
        ".x2m" => _decryptor2,
        ".x3m" => _decryptor3,
        _ => throw new NotSupportedException()
    };
}

file readonly struct Cipher(ReadOnlySpan<byte> key, ushort[] map) : IDecryptor
{
    private readonly NanoByteArray _key = new(key, PaddingMode.Circular);
    private readonly int _keySize = key.Length;
    private readonly ushort[] _map = map;

    public long Decrypt(Span<byte> data, long offset)
    {
        if (offset > _map.Length)
        {
            return long.MaxValue;
        }

        var buffer = (stackalloc byte[SimdHelper.GetPaddedLength(_map.Length)]);

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = data[_map[i]];
        }

        int step = SimdHelper.LaneCount;
        int offset_k;
        for (int i = 0; i < buffer.Length; i += step)
        {
            offset_k = (int)((offset + i) % _keySize);
            var window = buffer[i..(i + step)];
            var v = new Vector<byte>(window);
            var k = new Vector<byte>(_key[offset_k..(offset_k + step)]);
            (v ^ k).CopyTo(window);
        }

        buffer[(int)offset.._map.Length].CopyTo(data);

        return data.Length;
    }
}
