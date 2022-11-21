using System;
using MusicDecrypto.Library.Media;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed class TmDecrypto : DecryptoBase
{
    protected override IDecryptor Decryptor { get; init; } = new Cipher();

    public TmDecrypto(MarshalMemoryStream buffer, string name, WarnHandler? warn, AudioTypes type) : base(buffer, name, warn, type) { }

    private sealed class Cipher : IDecryptor
    {
        private static readonly byte[] _header = { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 };

        public void Decrypt(Span<byte> data, long offset)
        {
            if (offset == 0)
            {
                _header.CopyTo(data);
            }
        }
    }
}
