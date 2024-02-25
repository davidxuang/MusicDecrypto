using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using MusicDecrypto.Library.Media;
using MusicDecrypto.Library.Vendor.Tencent;
using static MusicDecrypto.Library.DecryptoBase;

namespace MusicDecrypto.Library;

public static class DecryptoFactory
{
    [Flags]
    public enum Vendors : uint
    {
        NetEase         = 1u << 0,
        Kugou           = 1u << 16,
        Kuwo            = 1u << 20,
        TencentTm       = 1u << 8,
        TencentQmc      = 1u << 9,
        Xiami           = 1u << 31,
        XimalayaDesktop = 1u << 5,
        XimalayaMobile  = 1u << 4,
    }

    private static readonly FrozenDictionary<string, (Vendors, AudioType)> _extensionMap = new Dictionary<string, (Vendors, AudioType)>()
    {
        { ".ncm",      (Vendors.NetEase,    AudioType.Undefined) },
        { ".tm2",      (Vendors.TencentTm,  AudioType.XM4a)      },
        { ".tm6",      (Vendors.TencentTm,  AudioType.XM4a)      },
        { ".qmc0",     (Vendors.TencentQmc, AudioType.Mpeg)      },
        { ".qmc3",     (Vendors.TencentQmc, AudioType.Mpeg)      },
        { ".qmc2",     (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".qmc4",     (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".qmc6",     (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".qmc8",     (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".tkm",      (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".qmcogg",   (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".qmcflac",  (Vendors.TencentQmc, AudioType.Flac)      },
        { ".bkcmp3",   (Vendors.TencentQmc, AudioType.Mpeg)      },
        { ".bkcm4a",   (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".bkcwma",   (Vendors.TencentQmc, AudioType.XWma)      },
        { ".bkcogg",   (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".bkcwav",   (Vendors.TencentQmc, AudioType.XWav)      },
        { ".bkcape",   (Vendors.TencentQmc, AudioType.XApe)      },
        { ".bkcflac",  (Vendors.TencentQmc, AudioType.Flac)      },
        { ".mgg",      (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".mgg1",     (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".mggl",     (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".mflac",    (Vendors.TencentQmc, AudioType.Flac)      },
        { ".mflac0",   (Vendors.TencentQmc, AudioType.Flac)      },
        { ".mmp4",     (Vendors.TencentQmc, AudioType.Mp4)       },
        { ".6d7033",   (Vendors.TencentQmc, AudioType.Mpeg)      },
        { ".6d3461",   (Vendors.TencentQmc, AudioType.XM4a)      },
        { ".6f6767",   (Vendors.TencentQmc, AudioType.Ogg)       },
        { ".776176",   (Vendors.TencentQmc, AudioType.XWav)      },
        { ".666c6163", (Vendors.TencentQmc, AudioType.Flac)      },
        { ".kgm",      (Vendors.Kugou,      AudioType.Undefined) },
        { ".kgma",     (Vendors.Kugou,      AudioType.Undefined) },
        { ".vpr",      (Vendors.Kugou,      AudioType.Undefined) },
        { ".kwm",      (Vendors.Kuwo,       AudioType.Undefined) },
        { ".mp3",      (Vendors.Xiami,      AudioType.Mpeg)      },
        { ".m4a",      (Vendors.Xiami,      AudioType.XM4a)      },
        { ".wav",      (Vendors.Xiami,      AudioType.XWav)      },
        { ".flac",     (Vendors.Xiami,      AudioType.Flac)      },
        { ".x2m",      (Vendors.XimalayaMobile, AudioType.Undefined) },
        { ".x3m",      (Vendors.XimalayaMobile, AudioType.Undefined) },
        { ".xm",       (Vendors.XimalayaDesktop | Vendors.Xiami, AudioType.Undefined) },
    }.ToFrozenDictionary();

    public static IReadOnlySet<string> KnownExtensions { get; } = _extensionMap.Keys.ToFrozenSet();

    public static DecryptoBase Create(
        MarshalMemoryStream buffer,
        string name,
        WarnHandler? warn = null)
    {
        if (TryCreate(buffer, name, warn, out var result, out var exceptions))
        {
            return result;
        }
        else
        {
            throw exceptions is null
                ? new NotSupportedException()
                : new NotSupportedException(string.Empty, new AggregateException(exceptions));
        }
    }

    private static bool TryCreate(
        MarshalMemoryStream buffer,
        string name,
        WarnHandler? warn,
        [NotNullWhen(true)] out DecryptoBase? decrypto,
        out IEnumerable<Exception>? exceptions)
    {
        var ext = Path.GetExtension(name);

        if (string.IsNullOrEmpty(ext))
        {
            decrypto = null;
            exceptions = null;
            return false;
        }

        // handle nested extension names
        if (TryCreate(buffer, Path.GetFileNameWithoutExtension(name), warn, out var result, out exceptions))
        {
            decrypto = result;
            return true;
        }

        (var vendors, var format) = _extensionMap.GetValueOrDefault(ext);

        decrypto = vendors switch
        {
            Vendors.NetEase    => new Vendor.NetEase.Decrypto(buffer, name, warn),
            Vendors.TencentTm  => new TmDecrypto(buffer, name, warn, format),
            Vendors.TencentQmc => new QmcDecrypto(buffer, name, warn, format),
            Vendors.Kugou      => new Vendor.Kugou.Decrypto(buffer, name, warn),
            Vendors.Kuwo       => new Vendor.Kuwo.Decrypto(buffer, name, warn),
            Vendors.Xiami      => new Vendor.Xiami.Decrypto(buffer, name, warn, format),
            Vendors.XimalayaMobile  => new Vendor.Ximalaya.MobileDecrypto(buffer, name, warn),
            Vendors.XimalayaDesktop => new Vendor.Ximalaya.DesktopDecrypto(buffer, name, warn),
            _                  => Enum.GetValues<Vendors>().Where(e => vendors.HasFlag(e)).CreateWithFallback(buffer, name, format, warn, out exceptions),
        };
        return decrypto is not null;
    }

    private static DecryptoBase? CreateWithFallback(
        this IEnumerable<Vendors> vendors,
        MarshalMemoryStream buffer,
        string name,
        AudioType format,
        WarnHandler? warn,
        out IEnumerable<Exception>? exceptions)
    {
        exceptions = null;
        Stack<Exception> stack = new();
        foreach (var vendor in vendors)
        {
            try
            {
                return vendor switch
                {
                    Vendors.NetEase    => new Vendor.NetEase.Decrypto(buffer, name, warn),
                    Vendors.TencentTm  => new TmDecrypto(buffer, name, warn, format),
                    Vendors.TencentQmc => new QmcDecrypto(buffer, name, warn, format),
                    Vendors.Kugou      => new Vendor.Kugou.Decrypto(buffer, name, warn),
                    Vendors.Kuwo       => new Vendor.Kuwo.Decrypto(buffer, name, warn),
                    Vendors.Xiami      => new Vendor.Xiami.Decrypto(buffer, name, warn, format),
                    Vendors.XimalayaMobile  => new Vendor.Ximalaya.MobileDecrypto(buffer, name, warn),
                    Vendors.XimalayaDesktop => new Vendor.Ximalaya.DesktopDecrypto(buffer, name, warn),
                    _ => throw new InvalidDataException(vendors.ToString()),
                };
            }
            catch (Exception e)
            {
                buffer.ResetPosition();
                stack.Push(e);
            }
        }
        exceptions = stack;
        return null;
    }
}
