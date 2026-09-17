namespace DiscordMemeBot.Models;

public class ClaimStore
{
    public List<UsernameClaim> Claims { get; set; } = new();
}

public class UsernameClaim
{
    public string DiscordUserId { get; set; } = string.Empty;
    public string DiscordDisplayName { get; set; } = string.Empty;
    public string InstagramUsername { get; set; } = string.Empty;
    public string? InstagramSenderId { get; set; }
    public DateTimeOffset ClaimedAt { get; set; }
}
