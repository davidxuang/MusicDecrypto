using System;
using System.IO;
using System.Linq;

namespace MusicDecrypto
{
    public enum MusicTypes
    {
        Flac,
        Mpeg,
        Ogg,
        XM4a,
        XWav,
        XWma,
    }

    public enum ImageTypes
    {
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
            MusicTypes.Ogg  => ".ogg",
            MusicTypes.XM4a => ".m4a",
            MusicTypes.XWav => ".wav",
            MusicTypes.XWma => ".wma",
            _ => throw new InvalidDataException("Undefined music type."),
        };

        public static MusicTypes? ParseMusicType(this byte[] data)
        {
            try
            {
                return Enum.GetValues<MusicTypes>()
                           .Where(type => type switch
                           {
                               //                                                                       f     L     a     C
                               MusicTypes.Flac => Enumerable.SequenceEqual(data.Take(4), new byte[] { 0x66, 0x4c, 0x61, 0x43 }),
                               //                                                                       I     D     3
                               MusicTypes.Mpeg => Enumerable.SequenceEqual(data.Take(3), new byte[] { 0x49, 0x44, 0x33 }),
                               //
                               MusicTypes.XWma => Enumerable.SequenceEqual(data.Take(16), new byte[] { 0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11, 0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c }),
                               _ => false
                           })
                           .Single();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public static string GetMime(this ImageTypes type) => type switch
        {
            ImageTypes.Gif  => "image/gif",
            ImageTypes.Jpeg => "image/jpeg",
            ImageTypes.Png  => "image/png",
            _ => throw new InvalidDataException("Undefined image type."),
        };

        public static ImageTypes? ParseImageType(this byte[] data)
        {
            try
            {
                return Enum.GetValues<ImageTypes>()
                           .Where(type => type switch
                           {
                           //                                                                       G     I     F     8
                           ImageTypes.Gif  => Enumerable.SequenceEqual(data.Take(4), new byte[] { 0x47, 0x49, 0x46, 0x38 }),
                           ImageTypes.Jpeg => Enumerable.SequenceEqual(data.Take(2), new byte[] { 0xff, 0xd8 }),
                           //                                                                             P     N     G
                           ImageTypes.Png  => Enumerable.SequenceEqual(data.Take(8), new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
                               _ => false
                           })
                           .Single();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
