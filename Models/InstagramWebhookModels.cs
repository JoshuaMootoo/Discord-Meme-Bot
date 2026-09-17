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
    public List<InstagramMessagingEvent> Messaging { get; set; } = new();
}

public class InstagramMessagingEvent
{
    public InstagramParty? Sender { get; set; }
    public InstagramParty? Recipient { get; set; }
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
