using System.Text.RegularExpressions;

namespace YaSpotBot;

internal partial class RegexHelper
{
    [GeneratedRegex(@"""accessToken""\s*:\s*""(?<token>[^""\\]*(?:\\.[^""\\]*)*)""")]
    public static partial Regex SpotifyToken();

    [GeneratedRegex(@"open\.spotify\.com/track/(?<id>[a-zA-Z0-9]+)")]
    public static partial Regex SpotifyTrackId();

    [GeneratedRegex(@"music\.yandex\.ru/album/(?<albumId>\d+)/track/(?<trackId>\d+)")]
    public static partial Regex YandexMusicIds();
}