using System;
using MusicDecrypto.Library.Media;
using static MusicDecrypto.Library.DecryptoBase;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed class TmDecrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type) : DecryptoBase(buffer, name, warn, null, type)
{
    protected override IDecryptor Decryptor { get; init; } = new Cipher();

    private sealed class Cipher : IDecryptor
    {
        private static readonly byte[] _header = [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70];

        public long Decrypt(Span<byte> data, long offset)
        {
            if (offset == 0)
            {
                _header.CopyTo(data);
            }
            return offset + data.Length;
        }
    }
}
