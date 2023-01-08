using static MusicDecrypto.Library.Vendor.Tencent.ApiClient.Album;

namespace MusicDecrypto.Library.Vendor.Tencent;

partial class ApiClient
{
    public record class Track(
        ulong Id,
        string Mid,
        string Name,
        string Title,
        TrackSinger[] Singer,
        TrackAlbum Album);
    public record class TrackSinger(
        ulong Id,
        string Mid,
        string Name,
        string Title,
        string Pmid);
    public record class TrackAlbum(
        ulong Id,
        string Mid,
        string Name,
        string Title,
        string Pmid);

    public record class Album(
        AlbumBasic BasicInfo,
        AlbumSingers Singer);
    public record class AlbumBasic(
        string AlbumID,
        string AlbumMid,
        string AlbumName,
        string Pmid);
    public record class AlbumSingers(
        AlbumSinger[] SingerList);
    public record class AlbumSinger(
        ulong SingerId,
        string Mid,
        string Name,
        string Role,
        string Pmid);
}
