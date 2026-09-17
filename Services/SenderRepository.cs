using DiscordMemeBot.Configuration;
using DiscordMemeBot.Models;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Services;

/// <summary>
/// Owns data/senders.json: the allow/pending/deny state for Instagram sender IDs.
/// All reads/writes go through a single semaphore since minimal API handlers run concurrently.
/// </summary>
public class SenderRepository
{
    private readonly string _path;
    private readonly InstagramOptions _instagramOptions;
    private readonly ILogger<SenderRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SenderRepository(IHostEnvironment env, IOptions<InstagramOptions> instagramOptions, ILogger<SenderRepository> logger)
    {
        _path = Path.Combine(env.ContentRootPath, "data", "senders.json");
        _instagramOptions = instagramOptions.Value;
        _logger = logger;
    }

    public async Task<SenderStatus> GetStatusAsync(string senderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            return StatusOf(store, senderId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Records (or refreshes) a pending entry for an unrecognized sender. No-ops if the sender
    /// is already allowed or denied.
    /// </summary>
    public async Task<PendingSender> RecordPendingShareAsync(string senderId, string? username, string lastSharedUrl, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            var existing = store.Pending.FirstOrDefault(p => p.SenderId == senderId);
            if (existing is null)
            {
                existing = new PendingSender
                {
                    SenderId = senderId,
                    Username = username,
                    FirstSeen = DateTimeOffset.UtcNow,
                    LastSharedUrl = lastSharedUrl
                };
                store.Pending.Add(existing);
            }
            else
            {
                existing.LastSharedUrl = lastSharedUrl;
                if (!string.IsNullOrEmpty(username))
                {
                    existing.Username = username;
                }
            }

            await JsonFileStore.SaveAsync(_path, store, ct);
            return existing;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ApproveAsync(string senderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            if (store.Allowed.Contains(senderId))
            {
                return true;
            }

            var pending = store.Pending.FirstOrDefault(p => p.SenderId == senderId);
            if (pending is null)
            {
                return false;
            }

            store.Pending.Remove(pending);
            store.Allowed.Add(senderId);
            if (!string.IsNullOrEmpty(pending.Username))
            {
                store.UsernameCache[senderId] = pending.Username;
            }

            await JsonFileStore.SaveAsync(_path, store, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DenyAsync(string senderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            if (store.Denied.Contains(senderId))
            {
                return true;
            }

            var pending = store.Pending.FirstOrDefault(p => p.SenderId == senderId);
            if (pending is null)
            {
                return false;
            }

            store.Pending.Remove(pending);
            store.Denied.Add(senderId);

            await JsonFileStore.SaveAsync(_path, store, ct);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<PendingSender>> GetPendingAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            return store.Pending.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetAllowedAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            return store.Allowed.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetDeniedAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            return store.Denied.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetCachedUsernameAsync(string senderId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            return store.UsernameCache.GetValueOrDefault(senderId)
                   ?? store.Pending.FirstOrDefault(p => p.SenderId == senderId)?.Username;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CacheUsernameAsync(string senderId, string username, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadAsync(ct);
            store.UsernameCache[senderId] = username;
            await JsonFileStore.SaveAsync(_path, store, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static SenderStatus StatusOf(SenderStore store, string senderId)
    {
        if (store.Denied.Contains(senderId))
        {
            return SenderStatus.Denied;
        }

        if (store.Allowed.Contains(senderId))
        {
            return SenderStatus.Allowed;
        }

        return SenderStatus.Unknown;
    }

    private async Task<SenderStore> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            var seeded = new SenderStore
            {
                Allowed = _instagramOptions.AllowedSenderIds.Distinct().ToList()
            };
            _logger.LogInformation("Creating {Path} seeded with {Count} allowed sender id(s) from configuration.", _path, seeded.Allowed.Count);
            await JsonFileStore.SaveAsync(_path, seeded, ct);
            return seeded;
        }

        return await JsonFileStore.LoadOrCreateAsync<SenderStore>(_path, ct);
    }
}
