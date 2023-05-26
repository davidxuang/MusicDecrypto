using System;
using System.Security.Cryptography;

namespace MusicDecrypto.Library.Cryptography.Extensions;

internal static class AesExtensions
{
    internal static int AesEcbDecrypt(this ReadOnlySpan<byte> cipher, byte[] key, Span<byte> destination)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptEcb(cipher, destination, PaddingMode.PKCS7);
    }
}
