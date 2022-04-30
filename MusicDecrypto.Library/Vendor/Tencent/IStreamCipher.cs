namespace MusicDecrypto.Library.Vendor.Tencent
{
    public interface IStreamCipher
    {
        public abstract void Decrypt(MarshalMemoryStream buffer);
    }
}
