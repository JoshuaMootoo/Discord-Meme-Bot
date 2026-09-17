using DiscordMemeBot.Models;

namespace DiscordMemeBot.Services;

/// <summary>
/// Turns a parsed Instagram webhook payload into Discord posts, applying the sender
/// allow/pending/deny gate and the Discord-user claim lookup along the way.
/// </summary>
public class InstagramShareProcessor
{
    private readonly SenderRepository _senders;
    private readonly ClaimRepository _claims;
    private readonly InstagramGraphClient _graphClient;
    private readonly DiscordNotifier _discordNotifier;
    private readonly ILogger<InstagramShareProcessor> _logger;

    public InstagramShareProcessor(
        SenderRepository senders,
        ClaimRepository claims,
        InstagramGraphClient graphClient,
        DiscordNotifier discordNotifier,
        ILogger<InstagramShareProcessor> logger)
    {
        _senders = senders;
        _claims = claims;
        _graphClient = graphClient;
        _discordNotifier = discordNotifier;
        _logger = logger;
    }

    public async Task ProcessAsync(InstagramWebhookPayload payload, CancellationToken ct)
    {
        foreach (var entry in payload.Entry)
        {
            foreach (var messagingEvent in entry.Messaging)
            {
                await ProcessMessagingEventAsync(messagingEvent, ct);
            }
        }
    }

    private async Task ProcessMessagingEventAsync(InstagramMessagingEvent messagingEvent, CancellationToken ct)
    {
        var senderId = messagingEvent.Sender?.Id;
        var message = messagingEvent.Message;
        if (string.IsNullOrEmpty(senderId) || message is null)
        {
            return;
        }

        var links = CollectLinks(message);
        if (links.Count == 0)
        {
            // Plain text DM with no recognized link, or an attachment type we don't handle
            // (e.g. story_mention) - nothing to gate or forward.
            return;
        }

        var status = await _senders.GetStatusAsync(senderId, ct);
        if (status == SenderStatus.Denied)
        {
            return;
        }

        if (status == SenderStatus.Unknown)
        {
            var username = await _graphClient.TryResolveUsernameAsync(senderId, ct);
            await _senders.RecordPendingShareAsync(senderId, username, links[0].Url, ct);
            _logger.LogInformation("Recorded pending share from unrecognized sender {SenderId} ({Username}).", senderId, username ?? "unresolved");
            return;
        }

        // Allowed: forward every link, resolving/caching the username for attribution as needed.
        var resolvedUsername = await ResolveAndCacheUsernameAsync(senderId, ct);
        var claim = await _claims.FindAndLockAsync(resolvedUsername, senderId, ct);

        foreach (var link in links)
        {
            var content = ComposeMessage(link, resolvedUsername, claim);
            await _discordNotifier.PostMessageAsync(content, ct);
        }
    }

    private async Task<string?> ResolveAndCacheUsernameAsync(string senderId, CancellationToken ct)
    {
        var cached = await _senders.GetCachedUsernameAsync(senderId, ct);
        if (!string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var resolved = await _graphClient.TryResolveUsernameAsync(senderId, ct);
        if (!string.IsNullOrEmpty(resolved))
        {
            await _senders.CacheUsernameAsync(senderId, resolved, ct);
        }

        return resolved;
    }

    private static List<SharedLink> CollectLinks(InstagramMessage message)
    {
        var links = new List<SharedLink>();

        if (message.Attachments is not null)
        {
            foreach (var attachment in message.Attachments)
            {
                if (attachment.Type is "share" or "ig_reel" && !string.IsNullOrEmpty(attachment.Payload?.Url))
                {
                    links.Add(new SharedLink(attachment.Payload.Url, LinkSource.Instagram));
                }
            }
        }

        if (!string.IsNullOrEmpty(message.Text))
        {
            links.AddRange(LinkExtractor.ExtractLinks(message.Text));
        }

        return links;
    }

    private static string ComposeMessage(SharedLink link, string? username, UsernameClaim? claim)
    {
        var (emoji, label) = link.Source switch
        {
            LinkSource.Instagram => ("\U0001F4F8", "Instagram"),
            LinkSource.TikTok => ("\U0001F3B5", "TikTok"),
            LinkSource.X => ("\U0001F426", "X"),
            _ => ("\U0001F517", "link")
        };

        if (claim is not null)
        {
            return $"{emoji} [{label}] Shared by <@{claim.DiscordUserId}> (@{claim.InstagramUsername}): {link.Url}";
        }

        if (!string.IsNullOrEmpty(username))
        {
            return $"{emoji} [{label}] Shared by @{username}: {link.Url}";
        }

        return $"{emoji} [{label}] New shared post: {link.Url}";
    }
}
