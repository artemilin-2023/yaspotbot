namespace YaSpotBot.Abstractions;

internal interface ISpotifyClientAuthorization
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}