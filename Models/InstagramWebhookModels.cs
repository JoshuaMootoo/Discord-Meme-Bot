using System.Text.Json.Serialization;

namespace DiscordMemeBot.Models;

public class InstagramWebhookPayload
{
    public string? Object { get; set; }
    public List<InstagramEntry> Entry { get; set; } = new();
}

public class InstagramEntry
{
    public string? Id { get; set; }
    public long Time { get; set; }

    /// <summary>"Instagram API with Facebook Login" (page-connected) shape.</summary>
    public List<InstagramMessagingEvent> Messaging { get; set; } = new();

    /// <summary>
    /// "Instagram API with Instagram Login" shape - the generic Graph API webhook envelope
    /// (field/value pairs) used when the app isn't connected via a Facebook Page. This is what
    /// Meta actually sends for apps set up this way, confirmed via the dashboard's Test button.
    /// </summary>
    public List<InstagramChange> Changes { get; set; } = new();
}

public class InstagramChange
{
    public string? Field { get; set; }
    public InstagramMessagingEvent? Value { get; set; }
}

public class InstagramMessagingEvent
{
    public InstagramParty? Sender { get; set; }
    public InstagramParty? Recipient { get; set; }

    // Meta sends this as a JSON string in the "Instagram Login" webhook shape (e.g. "1527459824"),
    // not a bare number, despite the spec's example showing it unquoted.
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long Timestamp { get; set; }

    public InstagramMessage? Message { get; set; }
}

public class InstagramParty
{
    public string? Id { get; set; }
}

public class InstagramMessage
{
    public string? Mid { get; set; }
    public string? Text { get; set; }
    public List<InstagramAttachment>? Attachments { get; set; }
}

public class InstagramAttachment
{
    public string? Type { get; set; }
    public InstagramAttachmentPayload? Payload { get; set; }
}

public class InstagramAttachmentPayload
{
    public string? Url { get; set; }
}
