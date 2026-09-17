using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordMemeBot.Models;

public static class DiscordInteractionType
{
    public const int Ping = 1;
    public const int ApplicationCommand = 2;
}

public static class DiscordInteractionResponseType
{
    public const int Pong = 1;
    public const int ChannelMessageWithSource = 4;
}

public class DiscordInteraction
{
    public int Type { get; set; }
    public DiscordInteractionData? Data { get; set; }
    public DiscordGuildMember? Member { get; set; }
    public DiscordUser? User { get; set; }

    /// <summary>The user who invoked the interaction, whether it came from a guild (member) or a DM.</summary>
    [JsonIgnore]
    public DiscordUser? InvokingUser => Member?.User ?? User;
}

public class DiscordGuildMember
{
    public DiscordUser? User { get; set; }
}

public class DiscordUser
{
    public string? Id { get; set; }
    public string? Username { get; set; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }

    [JsonIgnore]
    public string DisplayName => GlobalName ?? Username ?? "unknown";
}

public class DiscordInteractionData
{
    public string? Name { get; set; }
    public List<DiscordInteractionOption>? Options { get; set; }
}

public class DiscordInteractionOption
{
    public string? Name { get; set; }
    public JsonElement Value { get; set; }
}
