namespace DiscordMemeBot.Models;

public enum LinkSource
{
    Instagram,
    TikTok,
    X
}

public record SharedLink(string Url, LinkSource Source);
