using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicDecrypto.Library.Vendor;
using MusicDecrypto.Library.Vendor.Tencent;

namespace MusicDecrypto.Library.Tests
{
    [TestClass]
    public class TencentTests
    {
        [TestMethod]
        [DataRow("static")]
        [DataRow("map")]
        public void BasicTest(string type)
        {
            IStreamCipher cipher = type switch
            {
                "static" => new StaticCipher(),
                "map" => new MapCipher(Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray()),
                _ => throw new ArgumentException("Invalid cipher type.")
            };
            using var stream = new MarshalMemoryStream(16, false);
            stream.SetLength(16);
            cipher.Decrypt(stream);
            CollectionAssert.AreEqual(
                stream.AsSpan().ToArray(),
                type switch
                {
                    "static" => new byte[] {
                        0xc3, 0x4a, 0xd6, 0xcA, 0x90, 0x67, 0xf7, 0x52,
                        0xd8, 0xa1, 0x66, 0x62, 0x9f, 0x5b, 0x09, 0x00,
                    },
                    "map" => new byte[] {
                        0xbb, 0x7d, 0x80, 0xbe, 0xff, 0x38, 0x81, 0xfb,
                        0xbb, 0xff, 0x82, 0x3c, 0xff, 0xba, 0x83, 0x79,
                    },
                    _ => throw new ArgumentException("Invalid cipher type.")
                });
        }

        private static (string, byte[]) LoadKeyDataSet(string name)
        {
            MemoryStream enc = new(), plain = new();
            Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}_key_enc.bin")!.CopyTo(enc);
            Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}_key.bin")!.CopyTo(plain);
            return (Encoding.ASCII.GetString(enc.GetBuffer()), plain.GetBuffer());
        }

        [TestMethod]
        [DataRow("mflac", "map")]
        [DataRow("mgg", "map")]
        [DataRow("mflac", "rc4")]
        [DataRow("mflac0", "rc4")]
        public void FileKeyTest(string extension, string type)
        {
            string enc;
            byte[] plain;
            (enc, plain) = LoadKeyDataSet($"{extension}_{type}");
            CollectionAssert.AreEqual(
                TencentKey.DecryptKey(enc),
                plain);
            ;
        }

        private static (byte[], MarshalMemoryStream, byte[]) LoadFileDataSet(string name)
        {
            MemoryStream key = new(), plain = new();
            MarshalMemoryStream enc = new();
            Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}_key.bin")?.CopyTo(key);
            Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}_enc.bin")!.CopyTo(enc);
            Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.bin")!.CopyTo(plain);
            return (key.GetBuffer(), enc, plain.GetBuffer());
        }

        [TestMethod]
        [DataRow("qmc0", "static")]
        [DataRow("mflac", "map")]
        [DataRow("mgg", "map")]
        [DataRow("mflac", "rc4")]
        [DataRow("mflac0", "rc4")]
        public void FileTest(string extension, string type)
        {
            byte[] key, plain;
            (key, var stream, plain) = LoadFileDataSet($"{extension}_{type}");
            IStreamCipher cipher = type switch
            {
                "static" => new StaticCipher(),
                "map" => new MapCipher(key),
                "rc4" => new RC4Cipher(key, 5120, 128),
                _ => throw new ArgumentException("Invalid cipher type.")
            };
            cipher.Decrypt(stream);
            CollectionAssert.AreEqual(
                plain.AsSpan().ToArray(),
                plain);
            stream.Dispose();
        }
    }
}
