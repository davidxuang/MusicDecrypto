using System;
using System.IO;

namespace MusicDecrypto.Library.Media.Extensions;

internal static class MediaExtensions
{
    internal static string GetExtension(this AudioTypes type) => type switch
    {
        AudioTypes.Aac  => ".aac",
        AudioTypes.Flac => ".flac",
        AudioTypes.Mpeg => ".mp3",
        AudioTypes.Mp4  => ".m4a",
        AudioTypes.Ogg  => ".ogg",
        AudioTypes.XApe => ".ape",
        AudioTypes.XDff => ".dff",
        AudioTypes.XWav => ".wav",
        AudioTypes.XWma => ".wma",
        _ => throw new InvalidDataException("Undefined music type."),
    };

    internal static AudioTypes SniffAudioType(this MarshalMemoryStream data)
    {
        return data.AsSpan() switch
        {
            [0xff, 0xf1, ..]
                => AudioTypes.Aac,
            // f     L     a     C
            [0x66, 0x4c, 0x61, 0x43, ..]
                => AudioTypes.Flac,
            [0xff, 0xfa or 0xfb, ..]
                => AudioTypes.Mpeg,
            // I     D     3
            [0x49, 0x44, 0x33, ..]
                => AudioTypes.Mpeg,
            //             f     t     y     p     M     4     A
            [_, _, _, _, 0x66, 0x74, 0x79, 0x70/*0x4d, 0x34, 0x41, 0x20*/, ..]
                => AudioTypes.Mp4,
            // O     g     g     S
            [0x4f, 0x67, 0x67, 0x53, ..]
                => AudioTypes.Ogg,
            // M     A     C
            [0x4D, 0x41, 0x43, 0x20, ..]
                => AudioTypes.XApe,
            // F     R     M     8
            [0x46, 0x52, 0x4d, 0x38, ..]
                => AudioTypes.XDff,
            // R     I     F     F                 W     A     V     E
            [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x41, 0x56, 0x45, ..]
                => AudioTypes.XWav,
            [0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c, ..]
                => AudioTypes.XWma,
            _   => AudioTypes.Undefined
        };
    }

    internal static string GetMime(this ImageTypes type) => type switch
    {
        ImageTypes.Gif  => "image/gif",
        ImageTypes.Jpeg => "image/jpeg",
        ImageTypes.Png  => "image/png",
        _ => throw new InvalidDataException("Undefined image type."),
    };

    internal static ImageTypes SniffImageType(this byte[] data)
    {
        return data.AsSpan() switch
        {
            // G     I     F     8
            [0x47, 0x49, 0x46, 0x38, ..]
                => ImageTypes.Gif,
            [0xff, 0xd8, 0xff, 0xe0 or 0xe1, ..]
                => ImageTypes.Jpeg,
            //       P     N     G
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, ..]
                => ImageTypes.Png,
            _   => ImageTypes.Undefined
        };
    }
}
