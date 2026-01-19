using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using YaSpotBot.Abstractions;
using YaSpotBot.Models;

namespace YaSpotBot.Services;

internal class AnonymusSpotifyAuth(IHttpClientFactory clientFactory) : 
    ISpotifyClientAuthorization
{
    private const string TokenEndpoint = "https://open.spotify.com/embed/track/3n3Ppam7vgaVa1iaRUc9Lp";

    private readonly HttpClient _client = clientFactory.CreateClient();

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync(TokenEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = RegexHelper.SpotifyToken().Match(html);
        if (match.Success)
        {
            return match.Groups["token"].Value;
        }

        throw new InvalidOperationException("Failed to get token");
    }
}

internal class OfficialSpotifyAuth(IHttpClientFactory clientFactory, BotConfiguration configuration) : 
    ISpotifyClientAuthorization
{
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";

    private readonly HttpClient _client = clientFactory.CreateClient();
    private readonly BotConfiguration _configuration = configuration;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var request = BuildRequest();
        var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawPayload = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonDocument.Parse(rawPayload);

        return payload.RootElement.GetProperty("access_token").GetString()!;
    }

    private HttpRequestMessage BuildRequest()
    {
        var authHeader = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{_configuration.SpotifyClientId}:{_configuration.SpotifyClientSecret}"
            )
        );
        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        return request;
    }
}
