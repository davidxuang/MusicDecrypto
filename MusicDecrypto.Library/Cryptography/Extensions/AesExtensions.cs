using System.Security.Cryptography;

namespace MusicDecrypto.Library.Cryptography.Extensions
{
    internal static class AesExtensions
    {
        internal static byte[] AesEcbDecrypt(this byte[] cipher, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        }
    }
}
