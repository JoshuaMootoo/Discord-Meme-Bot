namespace DiscordMemeBot.Configuration;

public class DiscordOptions
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Guild (server) ID to register slash commands against for instant availability.</summary>
    public string GuildId { get; set; } = string.Empty;
}
