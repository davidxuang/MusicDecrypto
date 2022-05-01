using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;
using MusicDecrypto.Library.Cryptography;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Numerics;
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

            if (Simd.LaneCount % 8 != 0 || Simd.LaneCount <= 0)
                throw new InvalidOperationException("SIMD width should be at a multiple of 8.");

            var data = Convert.FromBase64String(value).AsSpan();
            int length = data.Length, step = Simd.LaneCount;

            if (length < 24)
                throw new InvalidDataException("Key is too small.");
            else if (length % 8 != 0)
                throw new InvalidDataException("Key is not in blocks of 8.");

            var raw = (stackalloc byte[length + step - 8]);
            var dst = (stackalloc byte[length + step - 8]);
            data.CopyTo(raw);
            data[8..].CopyTo(dst);

            var teaKey = (stackalloc byte[16]);
            for (int i = 0; i < 8; i++)
            {
                teaKey[2 * i] = (byte)(Math.Abs(Math.Tan(106 + i * 0.1)) * 100);
                teaKey[2 * i + 1] = raw[i];
            }

            var tea = new Tea(teaKey, 32);

            tea.DecryptBlock(dst[..8]);
            int padLength = dst[0] & 0x7;

            if (padLength + saltLength != 8)
                throw new InvalidDataException("Invalid padding length.");

            {
                var buf = (stackalloc byte[Simd.LaneCount]);

                for (int i = 8; i < length - 8; i += 8)
                {
                    var pre = dst[(i - 8)..];
                    var cur = dst[i..];

                    var x = new Vector<byte>(pre);
                    var y = new Vector<byte>(cur);
                    (x ^ y).CopyTo(buf);
                    buf[..8].CopyTo(cur);

                    tea.DecryptBlock(cur);
                }
            }

            for (int i = 8; i < length - 8; i += step)
            {
                var window = dst[i..(i + step)];

                var w = new Vector<byte>(window);
                var s = new Vector<byte>(raw[i..(i + step)]);
                (w ^ s).CopyTo(window);
            }

            foreach (var b in dst[(length - zeroLength)..length])
            {
                if (b != 0x00)
                    throw new InvalidDataException("Zero check failed.");
            }

            var key = dst[1..(length - padLength - saltLength - zeroLength)];
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
