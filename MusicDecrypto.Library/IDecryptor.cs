using System;

namespace MusicDecrypto.Library;

public interface IDecryptor
{
    public long Decrypt(Span<byte> data, long offset);

    public virtual long TrimStart => 0;
    // public virtual long TrimEnd => 0;
}
