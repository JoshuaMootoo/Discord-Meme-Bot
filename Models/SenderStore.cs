namespace DiscordMemeBot.Models;

public class SenderStore
{
    public List<string> Allowed { get; set; } = new();
    public List<string> Denied { get; set; } = new();
    public List<PendingSender> Pending { get; set; } = new();

    /// <summary>
    /// Instagram-scoped sender ID -> resolved username. Not part of the spec's
    /// documented shape but kept alongside it so approved senders can still be
    /// attributed by username without re-hitting the Graph API on every share.
    /// </summary>
    public Dictionary<string, string> UsernameCache { get; set; } = new();
}

public class PendingSender
{
    public string SenderId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public string? LastSharedUrl { get; set; }
}

public enum SenderStatus
{
    Unknown,
    Allowed,
    Denied
}
