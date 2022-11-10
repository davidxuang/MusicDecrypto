using System;

namespace MusicDecrypto.Library;

public interface IEncryptor
{
    public void Encrypt(Span<byte> data, long offset);
}
