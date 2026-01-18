using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using YaSpotBot.Abstractions;
using YaSpotBot.Models;

namespace YaSpotBot.Services.Clients;

internal class YandexClient(IHttpClientFactory clientFactory) : IMediaClient
{
    internal const string ClientName = nameof(YandexClient);

    private readonly HttpClient _client = clientFactory.CreateClient(ClientName);

    public async Task<TrackInfo> GetTrackInfo(string trackId, CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync($"tracks/{trackId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var trackResponse = await response.Content.ReadFromJsonAsync<YandexResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize Yandex track response.");

        return new TrackInfo()
        {
            Artist = trackResponse.Result.First().Artists.First().Name,
            SongName = trackResponse.Result.First().Title
        };
    }

    public async Task<string> GetUriAsync(TrackInfo info, CancellationToken cancellationToken)
    {
        var query = $"search?text={Uri.EscapeDataString($"{info.SongName} {info.Artist}")}&type=track&page=0";

        var response = await _client.GetAsync(query, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(payload);

        var track = doc.RootElement
            .GetProperty("result")
            .GetProperty("tracks")
            .GetProperty("results")
            .EnumerateArray()
            .First();

        var trackId = track.GetProperty("id").ToString();
        var albumId = track.GetProperty("albums").EnumerateArray().First().GetProperty("id").ToString();

        return $"https://music.yandex.ru/album/{albumId}/track/{trackId}";
    }

    private class YandexResponse
    {
        [JsonPropertyName("result")]
        public required YandexResultObject[] Result { get; set; }
    }

    private class YandexResultObject
    {
        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("artists")]
        public required YandexArtist[] Artists { get; set; }
    }

    private class YandexArtist
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }
}