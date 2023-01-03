using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicDecrypto.Library.Numerics;
using MusicDecrypto.Library.Vendor.Tencent;

namespace MusicDecrypto.Library.Tests;

[TestClass]
public class TencentTests
{
    [TestMethod]
    [DataRow("static")]
    [DataRow("map")]
    public void BasicTest(string type)
    {
        IDecryptor cipher = type switch
        {
            "static" => new StaticCipher(),
            "map" => new MapCipher(Enumerable.Range(0, 0x100).Select(x => (byte)x).ToArray()),
            _ => throw new ArgumentException("Invalid cipher type.")
        };
        var buffer = (stackalloc byte[SimdHelper.GetPaddedLength(16)]);
        cipher.Decrypt(buffer, 0);
        Assert.IsTrue(buffer[..16].SequenceEqual(
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
            }));
    }

    [TestMethod]
    [DataRow("mflac", "map")]
    [DataRow("mflac_v2", "map")]
    [DataRow("mgg", "map")]
    [DataRow("mflac", "rc4")]
    [DataRow("mflac0", "rc4")]
    public void KeyTest(string extension, string type)
    {
        string enc;
        byte[] plain;
        (enc, plain) = LoadKeyDataSet($"{extension}-{type}");
        CollectionAssert.AreEqual(
            QmcDecrypto.DecryptKey(enc),
            plain);
        ;
    }
    private static (string, byte[]) LoadKeyDataSet(string name)
    {
        MemoryStream enc = new(), plain = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.key.enc")!.CopyTo(enc);
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.key")!.CopyTo(plain);
        return (Encoding.ASCII.GetString(enc.GetBuffer()), plain.GetBuffer());
    }

    [TestMethod]
    [DataRow("qmc0", "static")]
    [DataRow("mflac", "map")]
    [DataRow("mgg", "map")]
    [DataRow("mflac", "rc4")]
    [DataRow("mflac0", "rc4")]
    public void PayloadTest(string extension, string type)
    {
        byte[] key, buffer, plain;
        (key, buffer, plain) = LoadPayloadDataSet($"{extension}-{type}");

        IDecryptor cipher = type switch
        {
            "static" => new StaticCipher(),
            "map" => new MapCipher(key),
            "rc4" => new RC4Cipher(key, 128, 5120),
            _ => throw new ArgumentException("Invalid cipher type.")
        };
        _ = cipher.Decrypt(buffer, 0);

        CollectionAssert.AreEqual(
            buffer,
            plain);
    }

    private static (byte[], byte[], byte[]) LoadPayloadDataSet(string name)
    {
        MemoryStream key = new(), enc = new(), plain = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.key")?.CopyTo(key);
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.payload.enc")!.CopyTo(enc);
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MusicDecrypto.Library.Tests.DataSets.{name}.payload")!.CopyTo(plain);
        return (key.GetBuffer(), enc.GetBuffer(), plain.GetBuffer());
    }
}
