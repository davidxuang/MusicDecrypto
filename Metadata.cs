using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MusicDecrypto
{
    internal class MemoryFileAbstraction : TagLib.File.IFileAbstraction
    {
        public MemoryFileAbstraction(string name, MemoryStream stream)
        {
            Name = name;
            ReadStream = stream;
            WriteStream = stream;
        }

        public string Name { get; private set; }
        public Stream ReadStream { get; private set; }
        public Stream WriteStream { get; private set; }
        // Prevent TagLib from disposing the stream to avoid conflicts
        public void CloseStream(Stream stream) { }
    }

    internal static class MediaType
    {
        public static string GetStreamMime(MemoryStream stream)
            => HeaderMatch(new byte[] { 0x66, 0x4c, 0x61, 0x43 }, stream)
            ? "audio/flac" //            f     L     a     C
            : HeaderMatch(new byte[] { 0x49, 0x44, 0x33 }, stream)
            ? "audio/mpeg" //            I     D     3
            : HeaderMatch(new byte[] { 0x4f, 0x67, 0x67, 0x53 }, stream)
            ? "audio/ogg" //             O     g     g     S
            : HeaderMatch(new byte[] { 0x47, 0x49, 0x46, 0x38 }, stream)
            ? "image/gif" //             G     I     F     8
            : HeaderMatch(new byte[] { 0xff, 0xd8 }, stream)
            ? "image/jpeg"
            : HeaderMatch(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, stream)
            ? "image/png" //                   P     N     G
            : null;
            
        private static bool HeaderMatch(byte[] target, MemoryStream stream)
        {
            if (target.Length > stream.Length) return false;
            return Enumerable.SequenceEqual(stream.ToArray().Take(target.Length).ToArray(), target);
        }

        public static string MimeToExt(string mime) => mime switch
        {
            "audio/flac" => "flac",
            "audio/ogg" => "ogg",
            "audio/mpeg" => "mp3",
            _ => null
        };
    }

    internal class Metadata
    {
        public string Title { get; set; }
        public string[] Artists { get; set; }
        public string Album { get; set; }
        public uint? Year { get; set; }
        public uint? Track { get; set; }
        public string Genre { get; set; }
        public string Comment { get; set; }
        public string AlbumArtist { get; set; }
        public string Composer { get; set; }
        public uint? Disc { get; set; }

        internal Metadata(NetEaseMetadata src)
        {
            Title = src.MusicName;
            if (src.Artist != null)
            {
                Artists = new string[src.Artist.Count];
                for (int i = 0; i < src.Artist.Count; i++)
                {
                    Artists[i] = src.Artist[i][0];
                }
                AlbumArtist = Artists[0];
            }
            Album = src.Album;
        }
    }

    internal class NetEaseMetadata
    {
        // public uint MusicId { get; set; }
        public string MusicName { get; set; }
        public List<List<string>> Artist { get; set; }
        // public uint AlbumId { get; set; }
        public string Album { get; set; }
        // public string AlbumPicDocId { get; set; }
        public string AlbumPic { get; set; }
        // public uint MvId { get; set; }
        // public uint Bitrate { get; set; }
        // public uint Duration { get; set; }
        // public string Format { get; set; }
    }
}
