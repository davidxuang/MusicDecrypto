using System;

namespace MusicDecrypto.Library;

public interface IEncryptor
{
    public long Encrypt(Span<byte> data, long offset);
}
