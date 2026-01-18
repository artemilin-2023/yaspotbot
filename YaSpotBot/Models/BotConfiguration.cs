namespace YaSpotBot.Models;

internal class BotConfiguration
{
    public required string Token { get; set; }
    public required string[] AllowedUsers { get; set; }
}
