using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using YaSpotBot.Abstractions;
using YaSpotBot.Models;
using YaSpotBot.Services;
using YaSpotBot.Services.Clients;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var botConfig = configuration.Get<BotConfiguration>() ?? throw new InvalidOperationException("Failed to load configuration");

var services = new ServiceCollection();

services.AddTransient<ISpotifyClientAuthorization, SpotifyClientAuthorization>();
services.AddTransient<SpotifyClient>();
services.AddTransient<YandexClient>();
services.AddTransient<SourceDecector>();
services.AddTransient<LinkMapper>();
services.AddHttpClient(SpotifyClient.ClientName, config =>
{
    config.BaseAddress = new Uri("https://api.spotify.com/v1/");
});
services.AddHttpClient(YandexClient.ClientName, config =>
{
    config.BaseAddress = new Uri("https://api.music.yandex.net/");
});

var sp = services.BuildServiceProvider();

var bot = new TelegramBotClient(botConfig.Token);
using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.InlineQuery]
};

bot.StartReceiving(
    HandleUpdate,
    HandleError,
    receiverOptions,
    cts.Token
);

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Log.Information("Bot started. Press Ctrl+C to stop.");
try
{
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(1000, cts.Token);
    }
}
catch (OperationCanceledException)
{
}

Log.Information("Stopping...");

async Task HandleUpdate(ITelegramBotClient client, Update update, CancellationToken ct)
{
    if (update.InlineQuery is not { } query) return;
    if (botConfig.AllowedUsers.Contains(query.From.Username) is false)
    {
        await client.AnswerInlineQuery(
            query.Id,
            [
                new InlineQueryResultArticle() 
                { 
                    Id = "0", 
                    Title = "Этот бот не для тебя, сори", 
                    InputMessageContent = new InputTextMessageContent("You do not have permission to use this bot.") 
                }
            ],
            cancellationToken: ct
        );
    }
    if (string.IsNullOrEmpty(query.Query)) return;

    var handler = sp.GetRequiredService<LinkMapper>();
    
    var results = await handler.ProcessAsync(query, ct);

    await client.AnswerInlineQuery(
        query.Id,
        results,
        cacheTime: 0,
        cancellationToken: ct
    );
}

Task HandleError(ITelegramBotClient client, Exception ex, CancellationToken ct)
{
    Log.Error(ex, "Handle some error: {Error}", ex.Message);
    return Task.CompletedTask;
}