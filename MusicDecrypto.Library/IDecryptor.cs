using System;

namespace MusicDecrypto.Library;

public interface IDecryptor
{
    public long Decrypt(Span<byte> data, long offset);
}
