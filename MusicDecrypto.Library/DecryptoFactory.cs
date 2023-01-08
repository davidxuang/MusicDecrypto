using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Vendor.Tencent;

namespace MusicDecrypto.Library;

public static class DecryptoFactory
{
    public enum Vendors : byte
    {
        NetEase    = 0x10,
        TencentTm  = 0x21,
        TencentQmc = 0x22,
        Kugou      = 0x30,
        Kuwo       = 0x40,
        Xiami      = 0x50,
        Ximalaya   = 0x60,
    }

    private static readonly Dictionary<string, ValueTuple<Vendors, AudioTypes>> _extensionMap = new()
    {
        { ".ncm",      (Vendors.NetEase,    AudioTypes.Undefined) },
        { ".tm2",      (Vendors.TencentTm,  AudioTypes.XM4a)      },
        { ".tm6",      (Vendors.TencentTm,  AudioTypes.XM4a)      },
        { ".qmc0",     (Vendors.TencentQmc, AudioTypes.Mpeg)      },
        { ".qmc3",     (Vendors.TencentQmc, AudioTypes.Mpeg)      },
        { ".qmc2",     (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".qmc4",     (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".qmc6",     (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".qmc8",     (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".tkm",      (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".qmcogg",   (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".qmcflac",  (Vendors.TencentQmc, AudioTypes.Flac)      },
        { ".bkcmp3",   (Vendors.TencentQmc, AudioTypes.Mpeg)      },
        { ".bkcm4a",   (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".bkcwma",   (Vendors.TencentQmc, AudioTypes.XWma)      },
        { ".bkcogg",   (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".bkcwav",   (Vendors.TencentQmc, AudioTypes.XWav)      },
        { ".bkcape",   (Vendors.TencentQmc, AudioTypes.XApe)      },
        { ".bkcflac",  (Vendors.TencentQmc, AudioTypes.Flac)      },
        { ".mgg",      (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".mgg1",     (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".mggl",     (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".mflac",    (Vendors.TencentQmc, AudioTypes.Flac)      },
        { ".mflac0",   (Vendors.TencentQmc, AudioTypes.Flac)      },
        { ".mmp4",     (Vendors.TencentQmc, AudioTypes.Mp4)       },
        { ".6d7033",   (Vendors.TencentQmc, AudioTypes.Mpeg)      },
        { ".6d3461",   (Vendors.TencentQmc, AudioTypes.XM4a)      },
        { ".6f6767",   (Vendors.TencentQmc, AudioTypes.Ogg)       },
        { ".776176",   (Vendors.TencentQmc, AudioTypes.XWav)      },
        { ".666c6163", (Vendors.TencentQmc, AudioTypes.Flac)      },
        { ".kgm",      (Vendors.Kugou,      AudioTypes.Undefined) },
        { ".kgma",     (Vendors.Kugou,      AudioTypes.Undefined) },
        { ".vpr",      (Vendors.Kugou,      AudioTypes.Undefined) },
        { ".kwm",      (Vendors.Kuwo,       AudioTypes.Undefined) },
        { ".xm",       (Vendors.Xiami,      AudioTypes.Undefined) },
        { ".mp3",      (Vendors.Xiami,      AudioTypes.Mpeg)      },
        { ".m4a",      (Vendors.Xiami,      AudioTypes.XM4a)       },
        { ".wav",      (Vendors.Xiami,      AudioTypes.XWav)      },
        { ".flac",     (Vendors.Xiami,      AudioTypes.Flac)      },
        { ".x2m",      (Vendors.Ximalaya,   AudioTypes.Undefined) },
        { ".x3m",      (Vendors.Ximalaya,   AudioTypes.Undefined) },
    };

    public static HashSet<string> KnownExtensions => _extensionMap.Keys.ToHashSet();

    public static DecryptoBase Create(
        MarshalMemoryStream buffer,
        string name,
        DecryptoBase.WarnHandler? warn = null,
        DecryptoBase.MatchRequestHandler? matchConfirm = null)
    {
        (var cipher, var format) = _extensionMap[Path.GetExtension(name)];
        return cipher switch
        {
            Vendors.NetEase    => new Vendor.NetEase.Decrypto(buffer, name, warn),
            Vendors.TencentTm  => new TmDecrypto(buffer, name, warn, format),
            Vendors.TencentQmc => new QmcDecrypto(buffer, name, warn, matchConfirm, format),
            Vendors.Kugou      => new Vendor.Kugou.Decrypto(buffer, name, warn),
            Vendors.Kuwo       => new Vendor.Kuwo.Decrypto(buffer, name, warn),
            Vendors.Xiami      => new Vendor.Xiami.Decrypto(buffer, name, warn, format),
            Vendors.Ximalaya   => new Vendor.Ximalaya.Decrypto(buffer, name, warn),
            _                  => throw new NotSupportedException(),
        };
    }
}
