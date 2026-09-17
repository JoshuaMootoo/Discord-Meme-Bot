namespace DiscordMemeBot.Configuration;

public class InstagramOptions
{
    public string VerifyToken { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public List<string> AllowedSenderIds { get; set; } = new();
}
