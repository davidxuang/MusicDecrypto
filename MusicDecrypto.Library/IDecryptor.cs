using System;

namespace MusicDecrypto.Library;

public interface IDecryptor
{
    public void Decrypt(Span<byte> data, long offset);
}
