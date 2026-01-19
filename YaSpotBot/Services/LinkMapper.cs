using Serilog;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using YaSpotBot.Services.Clients;

namespace YaSpotBot.Services;

internal class LinkMapper(SourceDecector sourceDecector, SpotifyClient spotifyClient, YandexClient yandexClient)
{
    private readonly SourceDecector _sourceDecector = sourceDecector;
    private readonly SpotifyClient _spotifyClient = spotifyClient;
    private readonly YandexClient _yandexClient = yandexClient;

    public async Task<List<InlineQueryResult>> ProcessAsync(InlineQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var sourceInfo = _sourceDecector.GetFromInput(query.Query);

            var trackInfo = await (sourceInfo.Type switch
            {
                SourceType.Spotify => _spotifyClient.GetTrackInfo(sourceInfo.Id, cancellationToken),
                SourceType.YandexMusic => _yandexClient.GetTrackInfo(sourceInfo.Id, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported source type"),
            });
             
            var resultUrl = await (sourceInfo.Type switch
            {
                SourceType.Spotify => _yandexClient.GetUriAsync(trackInfo, cancellationToken),
                SourceType.YandexMusic => _spotifyClient.GetUriAsync(trackInfo, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported source type"),
            });

            var sourcePlatform = sourceInfo.Type == SourceType.Spotify ? "Spotify" : "Яндекс.Музыка";
            var targetPlatform = sourceInfo.Type == SourceType.Spotify ? "Яндекс.Музыка" : "Spotify";

            return
            [
                new InlineQueryResultArticle(
                    id: Guid.NewGuid().ToString(),
                    title: $"{trackInfo.Artist} - {trackInfo.SongName}",
                    inputMessageContent: new InputTextMessageContent(
                        $"🎵 <b>{trackInfo.Artist}</b> - {trackInfo.SongName}\n\n" +
                        $"📍 Из: {sourcePlatform}\n" +
                        $"➡️ В: {targetPlatform}"
                    )
                    {
                        ParseMode = ParseMode.Html
                    }
                )
                {
                    Description = $"{sourcePlatform} → {targetPlatform}",
                    ReplyMarkup = new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithUrl($"Открыть в {targetPlatform}", resultUrl)
                    )
                }
            ];

        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
        {
            Log.Warning(ex, "Rate limit exceeded for query: {Query}", query.Query);
            return
            [
                new InlineQueryResultArticle(
                    id: Guid.NewGuid().ToString(),
                    title: "Превышен лимит запросов",
                    inputMessageContent: new InputTextMessageContent(
                        "Сервис временно недоступен из-за превышения лимита запросов. " +
                        "Пожалуйста, попробуйте снова через некоторое время."
                    )
                )
            ];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process link mapping for query: {Query}", query.Query);

            return
            [
                new InlineQueryResultArticle(
                    id: Guid.NewGuid().ToString(),
                    title: "Не удалось обработать запрос",
                    inputMessageContent: new InputTextMessageContent(
                        $"Не удалось сформировать ссылку для ввода '{query.Query}'. " +
                        $"Неверный формат ссылки или в конечном сервисе трек отсутствует."
                    )
                )
            ];
        }
    }
}

internal enum SourceType
{
    Spotify,
    YandexMusic
}

internal class SourceInfo
{
    public SourceType Type { get; set; }
    public required string Id { get; set; }
}

internal class SourceDecector
{
    public SourceInfo GetFromInput(string input)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out var _) is false)
            throw new InvalidOperationException("Input is not a valid url");

        var spotifyMatch = RegexHelper.SpotifyTrackId().Match(input);
        if (spotifyMatch.Success)
        {
            var trackId = spotifyMatch.Groups["id"].Value;
            return new SourceInfo()
            {
                Type = SourceType.Spotify,
                Id = trackId,
            };
        }

        var yandexMatch = RegexHelper.YandexMusicIds().Match(input);
        if (yandexMatch.Success)
        {
            var albumId = yandexMatch.Groups["albumId"].Value;
            var trackId = yandexMatch.Groups["trackId"].Value;

            return new SourceInfo()
            {
                Type = SourceType.YandexMusic,
                Id = $"{trackId}:{albumId}"
            };
        }

        throw new InvalidOperationException("Нуу ээээ нууу эээээ");
    }
}