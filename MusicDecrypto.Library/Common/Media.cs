using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicDecrypto.Library.Common
{
    public enum MusicTypes
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
        public static string GetExtension(this MusicTypes type) => type switch
        {
            MusicTypes.Flac => ".flac",
            MusicTypes.Mpeg => ".mp3",
            MusicTypes.Mp4  => ".m4a",
            MusicTypes.Ogg  => ".ogg",
            MusicTypes.XDsd => ".dff",
            MusicTypes.XWma => ".wma",
            MusicTypes.Wav  => ".wav",
            _ => throw new InvalidDataException("Undefined music type."),
        };

        public static MusicTypes SniffMusicType(this byte[] data)
        {
            try
            {
                return (Enum.GetValues(typeof(MusicTypes)) as IEnumerable<MusicTypes>)
                           .Where(type => type switch
                           {
                               //                                                           f     L     a     C
                               MusicTypes.Flac => data.Take(4).SequenceEqual(new byte[] { 0x66, 0x4c, 0x61, 0x43 }),
                               //                                                           I     D     3
                               MusicTypes.Mpeg => data.Take(3).SequenceEqual(new byte[] { 0x49, 0x44, 0x33 }),
                               //                                                           F     R     M     8
                               MusicTypes.XDsd => data.Take(4).SequenceEqual(new byte[] { 0x46, 0x52, 0x4d, 0x38 }),
                               MusicTypes.XWma => data.Take(16).SequenceEqual(new byte[] { 0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c }),
                               _ => false
                           })
                           .Single();
            }
            catch (InvalidOperationException)
            {
                return MusicTypes.Undefined;
            }
        }

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
                return (Enum.GetValues(typeof(ImageTypes)) as IEnumerable<ImageTypes>)
                           .Where(type => type switch
                           {
                           //                                                           G     I     F     8
                           ImageTypes.Gif  => data.Take(4).SequenceEqual(new byte[] { 0x47, 0x49, 0x46, 0x38 }),
                           ImageTypes.Jpeg => data.Take(2).SequenceEqual(new byte[] { 0xff, 0xd8 }),
                           //                                                                 P     N     G
                           ImageTypes.Png  => data.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
                               _ => false
                           })
                           .Single();
            }
            catch (InvalidOperationException)
            {
                return ImageTypes.Undefined;
            }
        }
    }
}
