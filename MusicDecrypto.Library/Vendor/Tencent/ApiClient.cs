using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using MusicDecrypto.Library.Helpers;

namespace MusicDecrypto.Library.Vendor.Tencent;

internal sealed partial class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly HttpClient _fcgiClient = new()
    {
        BaseAddress = new Uri("https://u.y.qq.com/cgi-bin/")
    };

    public ApiClient()
    {
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.8,en-US;q=0.6,en;q=0.5");
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(new("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.47.134 Safari/537.36 QBCore/3.53.47.400 QQBrowser/9.0.2524.400"));

        _fcgiClient.DefaultRequestHeaders.Accept.Add(new("*/*"));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new("zh-CN"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _fcgiClient?.Dispose();
    }

    private record class FastCgiRequests<T>(FastCgiRequest<T> Req);

    private record class FastCgiRequest<T>(
        string Method,
        string Module,
        T Param);

    private record class FastCgiResponses<R>(
        int Code,
        long Ts,
        long StartTs,
        string Traceid,
        FastCgiResponse<R> Req);

    private record class FastCgiResponse<R>(
        int Code,
        R? Data);

    private async ValueTask<R?> InvokeFastCgiCallAsync<T, R>(
        string module,
        string method,
        T param)
    {
        var response = await _fcgiClient.PostAsJsonAsync(
            $"musicu.fcg?pcachetime={DateTimeOffset.Now.ToUnixTimeSeconds()}",
            new FastCgiRequests<T>(new FastCgiRequest<T>(method, module, param)),
            TencentSerializerContext.Instance.GetTypeInfo<FastCgiRequests<T>>());
        var content = await response.Content.ReadFromJsonAsync(TencentSerializerContext.Instance.GetTypeInfo<FastCgiResponses<R>>());

        ThrowInvalidData.IfNull(content?.Traceid, "FastCGI call response");
        ThrowInvalidData.IfNotEqual(content?.Code ?? -1, 0, "FastCGI call response");
        ThrowInvalidData.IfNotEqual(content?.Req?.Code ?? -1, 0, "FastCGI call response");

        return content!.Req.Data;
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(FastCgiRequests<SearchParams>))]
    [JsonSerializable(typeof(FastCgiRequests<TrackInfoParams>))]
    [JsonSerializable(typeof(FastCgiRequests<AlbumInfoParams>))]
    [JsonSerializable(typeof(FastCgiResponses<SearchResponse>))]
    [JsonSerializable(typeof(FastCgiResponses<TrackInfoResponse>))]
    [JsonSerializable(typeof(FastCgiResponses<Album>))]
    private sealed partial class TencentSerializerContext : JsonSerializerContext
    {
        public static TencentSerializerContext Instance = new(
            new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

        public JsonTypeInfo<T> GetTypeInfo<T>()
        {
            return GetTypeInfo(typeof(T)) as JsonTypeInfo<T> ?? throw new InvalidCastException();
        }
    }
}
