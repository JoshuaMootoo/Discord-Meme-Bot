using System.Text.Json;
using DiscordMemeBot.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Services;

/// <summary>Resolves an Instagram-scoped sender ID to a username via the Graph API.</summary>
public class InstagramGraphClient
{
    private readonly HttpClient _httpClient;
    private readonly InstagramOptions _options;
    private readonly ILogger<InstagramGraphClient> _logger;

    public InstagramGraphClient(HttpClient httpClient, IOptions<InstagramOptions> options, ILogger<InstagramGraphClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Returns the resolved username, or null if it couldn't be resolved (e.g. missing token, API error).</summary>
    public async Task<string?> TryResolveUsernameAsync(string senderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.AccessToken))
        {
            _logger.LogWarning("Instagram:AccessToken is not configured; cannot resolve username for sender {SenderId}.", senderId);
            return null;
        }

        try
        {
            var url = $"https://graph.instagram.com/{Uri.EscapeDataString(senderId)}" +
                      $"?fields=username,name&access_token={Uri.EscapeDataString(_options.AccessToken)}";

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram Graph API returned {StatusCode} while resolving sender {SenderId}.",
                    (int)response.StatusCode, senderId);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("username", out var usernameElement))
            {
                return usernameElement.GetString();
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to resolve Instagram username for sender {SenderId}.", senderId);
            return null;
        }
    }
}
