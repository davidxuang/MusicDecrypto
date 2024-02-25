using System;
using System.IO;

namespace MusicDecrypto.Library.Media.Extensions;

internal static class MediaExtensions
{
    internal static string GetExtension(this AudioType type) => type switch
    {
        AudioType.Aac  => ".aac",
        AudioType.Flac => ".flac",
        AudioType.Mpeg => ".mp3",
        AudioType.Mp4  => ".mp4",
        AudioType.Ogg  => ".ogg",
        AudioType.XApe => ".ape",
        AudioType.XDff => ".dff",
        AudioType.XM4a => ".m4a",
        AudioType.XWav => ".wav",
        AudioType.XWma => ".wma",
        _ => throw new InvalidDataException("Undefined music type."),
    };

    internal static AudioType SniffAudioType(this Span<byte> data)
    {
        return data switch
        {
            [0xff, 0xf1, ..]
                => AudioType.Aac,
            // f     L     a     C
            [0x66, 0x4c, 0x61, 0x43, ..]
                => AudioType.Flac,
            [0xff, 0xfa or 0xfb, ..]
                => AudioType.Mpeg,
            // I     D     3
            [0x49, 0x44, 0x33, ..]
                => AudioType.Mpeg,
            //             f     t     y     p     M     4     A
            [_, _, _, _, 0x66, 0x74, 0x79, 0x70, 0x4d, 0x34, 0x41, 0x20, ..]
                => AudioType.XM4a,
            //             f     t     y     p
            [_, _, _, _, 0x66, 0x74, 0x79, 0x70, ..]
                => AudioType.Mp4,
            // O     g     g     S
            [0x4f, 0x67, 0x67, 0x53, ..]
                => AudioType.Ogg,
            // M     A     C
            [0x4D, 0x41, 0x43, 0x20, ..]
                => AudioType.XApe,
            // F     R     M     8
            [0x46, 0x52, 0x4d, 0x38, ..]
                => AudioType.XDff,
            // R     I     F     F                 W     A     V     E
            [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x41, 0x56, 0x45, ..]
                => AudioType.XWav,
            [0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c, ..]
                => AudioType.XWma,
            _   => AudioType.Undefined
        };
    }

    internal static string GetMime(this ImageType type) => type switch
    {
        ImageType.Gif  => "image/gif",
        ImageType.Jpeg => "image/jpeg",
        ImageType.Png  => "image/png",
        _ => throw new InvalidDataException("Undefined image type."),
    };

    internal static ImageType SniffImageType(this Span<byte> data)
    {
        return data switch
        {
            // G     I     F     8
            [0x47, 0x49, 0x46, 0x38, ..]
                => ImageType.Gif,
            [0xff, 0xd8, 0xff, 0xe0 or 0xe1, ..]
                => ImageType.Jpeg,
            //       P     N     G
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, ..]
                => ImageType.Png,
            _   => ImageType.Undefined
        };
    }
}
