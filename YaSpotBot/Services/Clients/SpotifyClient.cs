using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using YaSpotBot.Abstractions;
using YaSpotBot.Models;

namespace YaSpotBot.Services.Clients;

internal class SpotifyClient(IHttpClientFactory clientFactory, ISpotifyClientAuthorization authorization)
    : IMediaClient
{
    internal const string ClientName = nameof(SpotifyClient);

    private readonly HttpClient _client = clientFactory.CreateClient(ClientName);
    private readonly ISpotifyClientAuthorization _authorization = authorization;
    private string _token = string.Empty;

    public async Task<TrackInfo> GetTrackInfo(string trackId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_token))
        {
            await RefreshToken(cancellationToken);
        }

        var response = await _client.GetAsync($"tracks/{trackId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await RefreshToken(cancellationToken);
            response = await _client.GetAsync($"tracks/{trackId}", cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SpotifyResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to get spotify information");

        return new TrackInfo()
        {
            Artist = payload.Artists.First().Name,
            SongName = payload.SongName
        };
    }

    private async Task RefreshToken(CancellationToken cancellationToken)
    {
        _token = await _authorization.GetTokenAsync(cancellationToken);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _token
            );
    }

    public async Task<string> GetUriAsync(TrackInfo info, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_token))
        {
            await RefreshToken(cancellationToken);
        }

        var searchUrl = $"search?" +
            $"q=track:{Uri.EscapeDataString(info.SongName)} artist:{Uri.EscapeDataString(info.Artist)}" +
            $"&type=track&limit=1";

        var response = await _client.GetAsync(searchUrl, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await RefreshToken(cancellationToken);
            response = await _client.GetAsync(searchUrl, cancellationToken);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(payload);
        var items = doc.RootElement.GetProperty("tracks").GetProperty("items");

        if (items.GetArrayLength() > 0)
        {
            var trackUrl = items[0]
                .GetProperty("external_urls")
                .GetProperty("spotify")
                .GetString()
                ?? throw new InvalidOperationException("Failed to get spotify uri");

            return trackUrl;
        }

        throw new InvalidOperationException("Failed to get spotify uri");
    }

    private class SpotifyArtist
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }

    private class SpotifyResponse
    {
        [JsonPropertyName("artists")]
        public required SpotifyArtist[] Artists { get; set; }

        [JsonPropertyName("name")]
        public required string SongName { get; set; }
    }
}