using YaSpotBot.Abstractions;

namespace YaSpotBot.Services;

internal class SpotifyClientAuthorization(IHttpClientFactory clientFactory) : 
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
