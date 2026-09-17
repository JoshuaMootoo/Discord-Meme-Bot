using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiscordMemeBot.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Services;

/// <summary>
/// One-shot startup task that (re)registers the /claim, /unclaim and /whoami guild commands
/// with Discord. Guild commands (as opposed to global ones) propagate instantly, per the spec.
/// Registration is idempotent - Discord's bulk-overwrite endpoint just replaces the command set -
/// so this safely runs on every app start. Skipped entirely if the bot isn't configured yet.
/// </summary>
public class DiscordCommandRegistrationService : IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordCommandRegistrationService> _logger;

    public DiscordCommandRegistrationService(
        IHttpClientFactory httpClientFactory, IOptions<DiscordOptions> options, ILogger<DiscordCommandRegistrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.ApplicationId) ||
            string.IsNullOrEmpty(_options.BotToken) ||
            string.IsNullOrEmpty(_options.GuildId))
        {
            _logger.LogInformation(
                "Discord:ApplicationId / BotToken / GuildId not fully configured; skipping slash command registration.");
            return;
        }

        var commands = new object[]
        {
            new
            {
                name = "claim",
                description = "Claim an Instagram username as yours",
                options = new object[]
                {
                    new
                    {
                        type = 3, // STRING
                        name = "username",
                        description = "The Instagram username to claim",
                        required = true
                    }
                }
            },
            new
            {
                name = "unclaim",
                description = "Remove your Instagram username claim"
            },
            new
            {
                name = "whoami",
                description = "Show your current Instagram username claim"
            }
        };

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(DiscordCommandRegistrationService));
            client.BaseAddress = new Uri("https://discord.com/api/v10/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", _options.BotToken);

            var url = $"applications/{_options.ApplicationId}/guilds/{_options.GuildId}/commands";
            using var response = await client.PutAsJsonAsync(url, commands, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Registered Discord guild slash commands (/claim, /unclaim, /whoami).");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Discord slash command registration failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Discord slash command registration request failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
