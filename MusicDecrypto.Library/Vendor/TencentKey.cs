using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using MusicDecrypto.Library.Cryptography;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Vendor.Tencent;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class TencentKey : TencentBase
    {
        private IStreamCipher? _cipher;

        public TencentKey(MarshalMemoryStream buffer, string name, AudioTypes type) : base(buffer, name, type) { }

        public static byte[] DecryptKey(string value)
        {
            const int saltLength = 2;
            const int zeroLength = 7;

            var raw = Convert.FromBase64String(value).AsSpan();

            if (raw.Length < 24)
                throw new InvalidDataException("Key is too small.");
            else if (raw.Length % 8 != 0)
                throw new InvalidDataException("Key is not in blocks of 8.");

            var teaKey = (stackalloc byte[16]);
            for (int i = 0; i < 8; i++)
            {
                teaKey[2 * i] = (byte)(Math.Abs(Math.Tan(106 + i * 0.1)) * 100);
                teaKey[2 * i + 1] = raw[i];
            }

            var tea = new TEA(teaKey, 32);
            var dest = (stackalloc byte[8]);
            tea.Decrypt(raw[8..], dest);

            int padLength = dest[0] & 0x7;
            int keyLength = raw.Length - 1 - padLength - saltLength - zeroLength;

            if (padLength + saltLength != 8)
                throw new InvalidDataException("Invalid padding length.");

            var preIV = (stackalloc byte[8]);
            var curIV = raw[8..16];
            var key = (stackalloc byte[keyLength]);

            var rawIndex = 16;
            var destIndex = padLength + 1;

            for (int i = 0; i < saltLength; i++)
            {
                if (destIndex == 8)
                {
                    preIV = curIV;
                    curIV = raw[rawIndex..(rawIndex + 8)];

                    for (int j = 0; j < 8; j++)
                        dest[j] ^= raw[rawIndex + j];
                    tea.Decrypt(dest, dest);

                    rawIndex += 8;
                    destIndex = 0;
                }

                destIndex++;
            }

            for (int i = 8; i < keyLength; i++)
            {
                if (destIndex == 8)
                {
                    preIV = curIV;
                    curIV = raw[rawIndex..(rawIndex + 8)];

                    for (int j = 0; j < 8; j++)
                        dest[j] ^= raw[rawIndex + j];
                    tea.Decrypt(dest, dest);

                    rawIndex += 8;
                    destIndex = 0;
                }

                key[i] = (byte)(dest[destIndex] ^ preIV[destIndex]);
                destIndex++;
            }

            for (int i = 0; i <= zeroLength; i++)
            {
                if (dest[destIndex] != preIV[destIndex])
                    throw new InvalidDataException("Zero check failed");
            }

            raw[..8].CopyTo(key);
            return key.ToArray();
        }

        protected override void Process()
        {
            _ = _buffer.Seek(-4, SeekOrigin.End);
            int indicator = Reader.ReadInt32();

            byte[]? key = null;
            long length;

            if (indicator == 0x67615451) // "QTag"
            {
                int chunkLength = BinaryPrimitives.ReadInt32BigEndian(Reader.ReadBytes(4));
                length = _buffer.Length - 8 - chunkLength;
                var metas = Encoding.ASCII.GetString(_buffer.AsSpan((int)length, chunkLength)).Split(',');
                key = DecryptKey(metas[0]);
                // var id = ulong.Parse(metas[1]);
            }
            else if (indicator > 0 && indicator < 0x300)
            {
                length = _buffer.Length - 4 - indicator;
                try
                {
                    key = DecryptKey(Encoding.ASCII.GetString(_buffer.AsSpan((int)length, indicator)));
                }
                catch
                {
                    if (indicator == 0x225) throw new NotSupportedException("Unsupported new format.");
                    else throw;
                }
            }
            else
            {
                length = _buffer.Length - 4;
            }

            _buffer.SetLength(length);

            _cipher = key is null
                    ? new StaticCipher()
                    : key.Length > 300
                    ? new RC4Cipher(key, 5120, 128)
                    : new MapCipher(key);

            _buffer.ResetPosition();
            _cipher.Decrypt(_buffer);
        }
    }
}
