using System;
using System.Collections.Generic;
using System.IO;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Vendor.Tencent;

namespace MusicDecrypto.Library;

public static class DecryptoFactory
{
    public enum Vendors : byte
    {
        NetEase      = 0x10,
        TencentBasic = 0x21,
        TencentKey   = 0x22,
        Kugou        = 0x30,
        Kuwo         = 0x40,
        Xiami        = 0x50,
    }

    public static Dictionary<string, ValueTuple<Vendors, AudioTypes>> KnownExtensions => new()
    {
        { ".ncm",      (Vendors.NetEase,      AudioTypes.Undefined) },
        { ".tm2",      (Vendors.TencentBasic, AudioTypes.Mp4) },
        { ".tm6",      (Vendors.TencentBasic, AudioTypes.Mp4) },
        { ".qmc0",     (Vendors.TencentKey,   AudioTypes.Mpeg) },
        { ".qmc3",     (Vendors.TencentKey,   AudioTypes.Mpeg) },
        { ".qmc2",     (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".qmc4",     (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".qmc6",     (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".qmc8",     (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".tkm",      (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".qmcogg",   (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".qmcflac",  (Vendors.TencentKey,   AudioTypes.Flac) },
        { ".bkcmp3",   (Vendors.TencentKey,   AudioTypes.Mpeg) },
        { ".bkcm4a",   (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".bkcwma",   (Vendors.TencentKey,   AudioTypes.XWma) },
        { ".bkcogg",   (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".bkcwav",   (Vendors.TencentKey,   AudioTypes.XWav) },
        { ".bkcape",   (Vendors.TencentKey,   AudioTypes.XApe) },
        { ".bkcflac",  (Vendors.TencentKey,   AudioTypes.Flac) },
        { ".mgg",      (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".mgg1",     (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".mggl",     (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".mflac",    (Vendors.TencentKey,   AudioTypes.Flac) },
        { ".mflac0",   (Vendors.TencentKey,   AudioTypes.Flac) },
        { ".6d7033",   (Vendors.TencentKey,   AudioTypes.Mpeg) },
        { ".6d3461",   (Vendors.TencentKey,   AudioTypes.Mp4) },
        { ".6f6767",   (Vendors.TencentKey,   AudioTypes.Ogg) },
        { ".776176",   (Vendors.TencentKey,   AudioTypes.XWav) },
        { ".666c6163", (Vendors.TencentKey,   AudioTypes.Flac) },
        { ".kgm",      (Vendors.Kugou,        AudioTypes.Undefined) },
        { ".kgma",     (Vendors.Kugou,        AudioTypes.Undefined) },
        { ".vpr",      (Vendors.Kugou,        AudioTypes.Undefined) },
        { ".kwm",      (Vendors.Kuwo,         AudioTypes.Undefined) },
        { ".xm",       (Vendors.Xiami,        AudioTypes.Undefined) },
        { ".mp3",      (Vendors.Xiami,        AudioTypes.Mpeg) },
        { ".m4a",      (Vendors.Xiami,        AudioTypes.Mp4) },
        { ".wav",      (Vendors.Xiami,        AudioTypes.XWav) },
        { ".flac",     (Vendors.Xiami,        AudioTypes.Flac) },
    };

    public static DecryptoBase Create(MarshalMemoryStream buffer, string name, DecryptoBase.WarnHandler? warn = null)
    {
        (var cipher, var format) = KnownExtensions[Path.GetExtension(name)];
        return cipher switch
        {
            Vendors.NetEase      => new Vendor.NetEase.Decrypto(buffer, name, warn),
            Vendors.TencentBasic => new TmDecrypto(buffer, name, warn, format),
            Vendors.TencentKey   => new QmcDecrypto(buffer, name, warn, format),
            Vendors.Kugou        => new Vendor.Kugou.Decrypto(buffer, name, warn),
            Vendors.Kuwo         => new Vendor.Kuwo.Decrypto(buffer, name, warn),
            Vendors.Xiami        => new Vendor.Xiami.Decrypto(buffer, name, warn, format),
            _                    => throw new NotSupportedException(), // should not be accessed
        };
    }
}
