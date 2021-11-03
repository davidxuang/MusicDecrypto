using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicDecrypto.Library.Common
{
    public enum AudioTypes
    {
        Undefined,
        Flac,
        Mpeg,
        Mp4,
        Ogg,
        XDsd,
        XWma,
        Wav,
    }

    public enum ImageTypes
    {
        Undefined,
        Gif,
        Jpeg,
        Png,
    }

    public static class MediaTypeExtensions
    {
        private static readonly Dictionary<AudioTypes, byte[]> _audioHeaders = new Dictionary<AudioTypes, byte[]>
        {
            //                                f     L     a     C
            { AudioTypes.Flac, new byte[] { 0x66, 0x4c, 0x61, 0x43 } },
            //                                I     D     3
            { AudioTypes.Mpeg, new byte[] { 0x49, 0x44, 0x33 } },
            //                                F     R     M     8
            { AudioTypes.XDsd, new byte[] { 0x46, 0x52, 0x4d, 0x38 } },
            { AudioTypes.XWma, new byte[] { 0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c } },
        };
        private static readonly Dictionary<ImageTypes, byte[]> _imageHeaders = new Dictionary<ImageTypes, byte[]>
        {
            //                                G     I     F     8
            { ImageTypes.Gif,  new byte[] { 0x47, 0x49, 0x46, 0x38 } },
            { ImageTypes.Jpeg, new byte[] { 0xff, 0xd8 } },
            //                                P     N     G
            { ImageTypes.Png,  new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a } },
        };

        public static string GetExtension(this AudioTypes type) => type switch
        {
            AudioTypes.Flac => ".flac",
            AudioTypes.Mpeg => ".mp3",
            AudioTypes.Mp4  => ".m4a",
            AudioTypes.Ogg  => ".ogg",
            AudioTypes.XDsd => ".dff",
            AudioTypes.XWma => ".wma",
            AudioTypes.Wav  => ".wav",
            _ => throw new InvalidDataException("Undefined music type."),
        };

        public static AudioTypes SniffAudioType(this byte[] data)
        {
            try
            {
                return _audioHeaders.Keys.Single(type =>
                    {
                        byte[] header = _audioHeaders[type];
                        return data.AsSpan(0, header.Length).SequenceEqual(header);
                    });
            }
            catch (InvalidOperationException)
            {
                return AudioTypes.Undefined;
            }
        }
        public static AudioTypes SniffAudioType(this ExtendedMemoryStream data)
            => data.ToArray(16).SniffAudioType();

        public static string GetMime(this ImageTypes type) => type switch
        {
            ImageTypes.Gif  => "image/gif",
            ImageTypes.Jpeg => "image/jpeg",
            ImageTypes.Png  => "image/png",
            _ => throw new InvalidDataException("Undefined image type."),
        };

        public static ImageTypes SniffImageType(this byte[] data)
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
}
