using System;
using System.IO;
using System.Reflection;
using MusicDecrypto.Library.Media;
using static MusicDecrypto.Library.DecryptoBase;

namespace MusicDecrypto.Library.Vendor.Ximalaya;

internal sealed partial class Decrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type = AudioTypes.Undefined) : DecryptoBase(buffer, name, warn, null, type)
{
    private static readonly byte[] _key2 = "xmly"u8.ToArray();
    private static readonly byte[] _key3 = "3989d111aad5613940f4fc44b639b292"u8.ToArray();
    private static readonly IDecryptor _decryptor2;
    private static readonly IDecryptor _decryptor3;

    static Decrypto()
    {
        using (var _stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("MusicDecrypto.Library.Vendor.Ximalaya.x2m_map.bin")!)
        {
            using var _reader2 = new BinaryReader(_stream2);
            var _map2 = new ushort[(int)(_stream2.Length / 2)];
            for (int i = 0; i < _map2.Length; i++)
            {
                _map2[i] = _reader2.ReadUInt16();
            }
            _decryptor2 = new Cipher(_key2, _map2);
        }

        using (var _stream3 = Assembly.GetExecutingAssembly().GetManifestResourceStream("MusicDecrypto.Library.Vendor.Ximalaya.x3m_map.bin")!)
        {
            using var _reader3 = new BinaryReader(_stream3);
            var _map3 = new ushort[(int)(_stream3.Length / 2)];
            for (int i = 0; i < _map3.Length; i++)
            {
                _map3[i] = _reader3.ReadUInt16();
            }
            _decryptor3 = new Cipher(_key3, _map3);
        }
    }

    protected override IDecryptor Decryptor { get; init; } = Path.GetExtension(name) switch
    {
        ".x2m" => _decryptor2,
        ".x3m" => _decryptor3,
        _ => throw new NotSupportedException()
    };
}
