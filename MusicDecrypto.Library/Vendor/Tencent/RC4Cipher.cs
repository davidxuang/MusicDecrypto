using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Vendor.Tencent
{
    public sealed class RC4Cipher : IStreamCipher
    {
        private readonly byte[] _key;
        private readonly byte[] _box;
        private readonly int _blockSize;
        private readonly int _headerSize;
        private readonly int _keySize;
        private readonly uint _hash;

        public RC4Cipher(byte[] key, int blockSize, int headerSize)
        {
            _key = key;
            _keySize = _key.Length;
            _blockSize = blockSize;
            _headerSize = headerSize;

            if (_keySize == 0)
                throw new ArgumentException("Key is empty.");

            _box = Enumerable.Range(0, _keySize).Select(x => (byte)x).ToArray();

            int j = 0;
            for (int i = 0; i < _keySize; i++)
            {
                j = (j + _box[i] + _key[i]) % _keySize;
                (_box[i], _box[j]) = (_box[j], _box[i]);
            }

            _hash = 1;
            for (int i = 0; j < _keySize; i++)
            {
                if (key[i] == 0)
                    continue;
                uint next = _hash * key[i];
                if (next == 0 || next <= _hash)
                    break;
                _hash = next;
            }
        }

        public void Decrypt(MarshalMemoryStream buffer)
            => Encrypt(buffer);

        public void Encrypt(MarshalMemoryStream buffer)
        {
            int offset = 0;
            int ahead = (int)buffer.Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool UpdateStats(int size)
            {
                offset += size;
                ahead -= size;
                return ahead == 0;
            };

            int blockLength = Math.Min(_headerSize, ahead);
            EncryptHeader(buffer.AsSpan(0, blockLength));
            if (UpdateStats(blockLength))
                return;

            if (offset % _blockSize != 0)
            {
                blockLength = Math.Min(_blockSize - offset % _blockSize, ahead);
                EncryptBlock(buffer.AsSpan(offset, blockLength), offset);
                if (UpdateStats(blockLength))
                    return;
            }

            while (ahead > 0)
            {
                var size = Math.Min(_blockSize, ahead);
                EncryptBlock(buffer.AsSpan(offset, size), offset);
                UpdateStats(size);
            }
        }

        private void EncryptHeader(Span<byte> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] ^= _key[GetOffset(i)];
        }

        private void EncryptBlock(Span<byte> buffer, int offset)
        {
            var box = (stackalloc byte[_box.Length]);
            _box.CopyTo(box);
            int j = 0, k = 0;
            int skipLength = (offset % _blockSize) + GetOffset(offset / _blockSize);

            for (int i = -skipLength; i < buffer.Length; i++)
            {
                j = (j + 1) % _keySize;
                k = (box[j] + k) % _keySize;
                (box[j], box[k]) = (box[k], box[j]);
                if (i >= 0) buffer[i] ^= box[(box[j] + box[k]) % _keySize];
            }
        }

        private int GetOffset(int index)
        {
            long sum = (long)(_hash / (double)((index + 1) * _key[index % _keySize]) * 100);
            return (int)(sum % _keySize);
        }
    }
}
