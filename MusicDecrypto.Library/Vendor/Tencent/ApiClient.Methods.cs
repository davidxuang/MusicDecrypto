using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicDecrypto.Library.Vendor.Tencent;

partial class ApiClient
{
    public async ValueTask<byte[]> GetAlbumCoverByIdAsync(ulong id)
    {
        return await _httpClient.GetByteArrayAsync($"https://imgcache.qq.com/music/photo/album/{id % 100}/albumpic_{id}_0.jpg");
    }

    public async Task<byte[]> GetAlbumCoverByMediaIdAsync(string mediaId)
    {
        return await _httpClient.GetByteArrayAsync($"https://y.gtimg.cn/music/photo_new/T002R500x500M000{mediaId}.jpg");
    }

    private record class SearchParams(
        int Grp,
        int NumPerPage,
        int PageNum,
        string Query,
        string Remoteplace,
        int SearchType);

    private record class SearchResponse(
        int Code,
        SearchResponseBody Body);
    private record class SearchResponseBody(SearchResponseSong Song);
    private record class SearchResponseSong(Track[] List);

    public async ValueTask<Track[]> SearchAsync(string keyword)
    {
        var content = await InvokeFastCgiCallAsync<SearchParams, SearchResponse>(
            "music.search.SearchCgiService",
            "DoSearchForQQMusicDesktop",
            new(1, 40, 1, keyword, "sizer.newclient.song", 0)
        );

        return content!.Body.Song.List;
    }

    private record class TrackInfoParams(
        ulong[] Ids,
        int[] Types);

    private record class TrackInfoResponse(Track[] Tracks);

    public async ValueTask<Track[]?> GetTracksInfoAsync(params ulong[] ids)
    {
        var content = await InvokeFastCgiCallAsync<TrackInfoParams, TrackInfoResponse>(
            "music.trackInfo.UniformRuleCtrl",
            "CgiGetTrackInfo",
            new(ids, new[] { 0 }));

        return content?.Tracks;
    }

    private record class AlbumInfoParams([property:JsonPropertyName("albumMid")] string AlbumMid);

    public async Task<Album?> GetAlbumInfoAsync(string mid)
    {
        return await InvokeFastCgiCallAsync<AlbumInfoParams, Album>(
            "music.musichallAlbum.AlbumInfoServer",
            "GetAlbumDetail",
            new(mid));
    }
}
