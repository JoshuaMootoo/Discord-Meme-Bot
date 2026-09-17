using System.Net.Http.Json;
using DiscordMemeBot.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Services;

/// <summary>Posts a already-composed message to the configured Discord channel webhook.</summary>
public class DiscordNotifier
{
    private readonly HttpClient _httpClient;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordNotifier> _logger;

    public DiscordNotifier(HttpClient httpClient, IOptions<DiscordOptions> options, ILogger<DiscordNotifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PostMessageAsync(string content, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.WebhookUrl))
        {
            _logger.LogWarning("Discord:WebhookUrl is not configured; dropping message: {Content}", content);
            return;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, new { content }, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Discord webhook post failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Discord webhook post failed.");
        }
    }
}
