using System;
using System.Buffers.Binary;

namespace MusicDecrypto.Library.Cryptography;

internal readonly struct Tea
{
    private const int _sizeBlock = 8;
    private const int _sizeKey = 16;
    private const uint _delta = 0x9e3779b9;

    private readonly uint[] _key;
    private readonly uint _rounds;

    /// <exception cref="ArgumentException">Thrown when any of the arguments is illegal.</exception>
    internal Tea(ReadOnlySpan<byte> key, uint rounds = 64)
    {
        if (key.Length != _sizeKey)
            throw new ArgumentException($"Key length should be {_sizeKey}. (got {key.Length})", nameof(key));
        else if (rounds % 1 != 0)
            throw new ArgumentException($"Round count should be even. (got {rounds})", nameof(rounds));

        _key =
        [
            BinaryPrimitives.ReadUInt32BigEndian(key[..4]),
            BinaryPrimitives.ReadUInt32BigEndian(key[4..8]),
            BinaryPrimitives.ReadUInt32BigEndian(key[8..12]),
            BinaryPrimitives.ReadUInt32BigEndian(key[12..]),
        ];
        _rounds = rounds;
    }

    /// <exception cref="ArgumentException">Thown when buffer length is insufficient.</exception>
    internal readonly void DecryptBlock(Span<byte> buffer)
    {
        if (buffer.Length < _sizeBlock)
            throw new ArgumentException($"Decryption buffer size should be {_sizeBlock} at least. (got {buffer.Length})", nameof(buffer));

        uint vl = BinaryPrimitives.ReadUInt32BigEndian(buffer[..4]);
        uint vh = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..8]);

        uint sum = _delta * (_rounds / 2);

        for (int i = 0; i < _rounds / 2; i++)
        {
            vh -= ((vl << 4) + _key[2]) ^ (vl + sum) ^ ((vl >> 5) + _key[3]);
            vl -= ((vh << 4) + _key[0]) ^ (vh + sum) ^ ((vh >> 5) + _key[1]);
            sum -= _delta;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[..4], vl);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..8], vh);
    }
}
