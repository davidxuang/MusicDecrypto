using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicDecrypto.Library.Media.Extensions;

internal static class MediaExtensions
{
    private static readonly Dictionary<AudioTypes, ValueTuple<int, byte[]>> _audioHeaders = new()
    {
        { AudioTypes.Aac,  (0, new byte[] { 0xff, 0xf1 }) },
        //                                    f     L     a     C
        { AudioTypes.Flac, (0, new byte[] { 0x66, 0x4c, 0x61, 0x43 }) },
        //                                    I     D     3
        { AudioTypes.Mpeg, (0, new byte[] { 0x49, 0x44, 0x33 }) },
        //                                    f     t     y     p     M     4     A
        { AudioTypes.Mp4,  (4, new byte[] { 0x66, 0x74, 0x79, 0x70, /*0x4d, 0x34, 0x41, 0x20*/ }) },
        //                                    O     g     g     S
        { AudioTypes.Ogg,  (0, new byte[] { 0x4f, 0x67, 0x67, 0x53 }) },
        //                                    F     R     M     8
        { AudioTypes.XDff, (0, new byte[] { 0x46, 0x52, 0x4d, 0x38 }) },
        { AudioTypes.XWma, (0, new byte[] { 0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c }) },
    };
    private static readonly Dictionary<ImageTypes, byte[]> _imageHeaders = new()
    {
        //                                G     I     F     8
        { ImageTypes.Gif, new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        { ImageTypes.Jpeg, new byte[] { 0xff, 0xd8 } },
        //                                P     N     G
        { ImageTypes.Png, new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a } },
    };

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
        try
        {
            return _audioHeaders.Keys.Single(type =>
            {
                byte[] header = _audioHeaders[type].Item2;
                return data.AsSpan(_audioHeaders[type].Item1, header.Length).SequenceEqual(header);
            });
        }
        catch (InvalidOperationException)
        {
            return AudioTypes.Undefined;
        }
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
        try
        {
            return _imageHeaders.Keys.Single(type =>
                {
                    byte[] header = _imageHeaders[type];
                    return data.AsSpan(0, header.Length).SequenceEqual(header);
                });
        }
        catch (InvalidOperationException)
        {
            return ImageTypes.Undefined;
        }
    }
}
