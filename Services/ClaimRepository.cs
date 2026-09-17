using DiscordMemeBot.Models;

namespace DiscordMemeBot.Services;

/// <summary>Owns data/claims.json: the Discord user &lt;-&gt; Instagram username claim mappings.</summary>
public class ClaimRepository
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ClaimRepository(IHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "data", "claims.json");
    }

    public async Task<UsernameClaim?> GetByDiscordUserAsync(string discordUserId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await JsonFileStore.LoadOrCreateAsync<ClaimStore>(_path, ct);
            return store.Claims.FirstOrDefault(c => c.DiscordUserId == discordUserId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Looks up a claim by resolved Instagram username or, once locked, by sender ID.
    /// If a username match is found with no sender ID locked yet, it is locked to
    /// <paramref name="senderId"/> and persisted before returning.
    /// </summary>
    public async Task<UsernameClaim?> FindAndLockAsync(string? username, string senderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await JsonFileStore.LoadOrCreateAsync<ClaimStore>(_path, ct);

            var bySenderId = store.Claims.FirstOrDefault(c => c.InstagramSenderId == senderId);
            if (bySenderId is not null)
            {
                return bySenderId;
            }

            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            var byUsername = store.Claims.FirstOrDefault(c =>
                c.InstagramUsername.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (byUsername is null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(byUsername.InstagramSenderId))
            {
                byUsername.InstagramSenderId = senderId;
                await JsonFileStore.SaveAsync(_path, store, ct);
            }

            return byUsername;
        }
        finally
        {
            _lock.Release();
        }
    }

    public enum ClaimResult
    {
        Created,
        Updated,
        UsernameTakenByOther
    }

    public async Task<(ClaimResult Result, UsernameClaim Claim)> ClaimAsync(
        string discordUserId, string discordDisplayName, string instagramUsername, CancellationToken ct = default)
    {
        instagramUsername = instagramUsername.Trim().ToLowerInvariant();

        await _lock.WaitAsync(ct);
        try
        {
            var store = await JsonFileStore.LoadOrCreateAsync<ClaimStore>(_path, ct);

            var takenByOther = store.Claims.FirstOrDefault(c =>
                c.InstagramUsername.Equals(instagramUsername, StringComparison.OrdinalIgnoreCase) &&
                c.DiscordUserId != discordUserId);
            if (takenByOther is not null)
            {
                return (ClaimResult.UsernameTakenByOther, takenByOther);
            }

            var existing = store.Claims.FirstOrDefault(c => c.DiscordUserId == discordUserId);
            if (existing is not null)
            {
                var usernameChanged = !existing.InstagramUsername.Equals(instagramUsername, StringComparison.OrdinalIgnoreCase);
                existing.InstagramUsername = instagramUsername;
                existing.DiscordDisplayName = discordDisplayName;
                existing.ClaimedAt = DateTimeOffset.UtcNow;
                if (usernameChanged)
                {
                    // The old sender-ID lock was tied to the previous username; re-derive it later.
                    existing.InstagramSenderId = null;
                }

                await JsonFileStore.SaveAsync(_path, store, ct);
                return (ClaimResult.Updated, existing);
            }

            var claim = new UsernameClaim
            {
                DiscordUserId = discordUserId,
                DiscordDisplayName = discordDisplayName,
                InstagramUsername = instagramUsername,
                InstagramSenderId = null,
                ClaimedAt = DateTimeOffset.UtcNow
            };
            store.Claims.Add(claim);

            await JsonFileStore.SaveAsync(_path, store, ct);
            return (ClaimResult.Created, claim);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UnclaimAsync(string discordUserId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await JsonFileStore.LoadOrCreateAsync<ClaimStore>(_path, ct);
            var existing = store.Claims.FirstOrDefault(c => c.DiscordUserId == discordUserId);
            if (existing is null)
            {
                return false;
            }

            store.Claims.Remove(existing);
            await JsonFileStore.SaveAsync(_path, store, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
