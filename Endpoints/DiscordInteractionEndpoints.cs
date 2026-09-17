using System.Text.Json;
using DiscordMemeBot.Configuration;
using DiscordMemeBot.Models;
using DiscordMemeBot.Services;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Endpoints;

public static class DiscordInteractionEndpoints
{
    private const int EphemeralFlag = 64;

    public static void MapDiscordInteractionEndpoints(this WebApplication app)
    {
        app.MapPost("/discord-interactions", async (
            HttpContext context,
            ClaimRepository claims,
            IOptions<DiscordOptions> options,
            ILogger<Program> logger) =>
        {
            using var bodyStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(bodyStream, context.RequestAborted);
            var bodyBytes = bodyStream.ToArray();

            var signature = context.Request.Headers["X-Signature-Ed25519"].ToString();
            var timestamp = context.Request.Headers["X-Signature-Timestamp"].ToString();

            if (!DiscordSignatureVerifier.Verify(bodyBytes, signature, timestamp, options.Value.PublicKey))
            {
                logger.LogWarning("Rejected Discord interaction with invalid Ed25519 signature.");
                return Results.Unauthorized();
            }

            var interaction = JsonSerializer.Deserialize<DiscordInteraction>(bodyBytes, JsonFileStore.Options);
            if (interaction is null)
            {
                return Results.BadRequest();
            }

            if (interaction.Type == DiscordInteractionType.Ping)
            {
                return Results.Json(new { type = DiscordInteractionResponseType.Pong });
            }

            if (interaction.Type == DiscordInteractionType.ApplicationCommand)
            {
                var content = await HandleCommandAsync(interaction, claims, context.RequestAborted);
                return Results.Json(new
                {
                    type = DiscordInteractionResponseType.ChannelMessageWithSource,
                    data = new { content, flags = EphemeralFlag }
                });
            }

            return Results.Ok();
        });
    }

    private static async Task<string> HandleCommandAsync(DiscordInteraction interaction, ClaimRepository claims, CancellationToken ct)
    {
        var user = interaction.InvokingUser;
        if (string.IsNullOrEmpty(user?.Id))
        {
            return "Couldn't identify who ran this command.";
        }

        return interaction.Data?.Name switch
        {
            "claim" => await HandleClaimAsync(interaction, user, claims, ct),
            "unclaim" => await HandleUnclaimAsync(user, claims, ct),
            "whoami" => await HandleWhoAmIAsync(user, claims, ct),
            _ => "Unknown command."
        };
    }

    private static async Task<string> HandleClaimAsync(DiscordInteraction interaction, DiscordUser user, ClaimRepository claims, CancellationToken ct)
    {
        var usernameOption = interaction.Data?.Options?.FirstOrDefault(o => o.Name == "username");
        var username = usernameOption?.Value.GetString();
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Please provide an Instagram username, e.g. `/claim username:dave_memes`.";
        }

        var (result, claim) = await claims.ClaimAsync(user.Id!, user.DisplayName, username, ct);
        return result switch
        {
            ClaimRepository.ClaimResult.UsernameTakenByOther =>
                $"❌ @{claim.InstagramUsername} is already claimed by <@{claim.DiscordUserId}>.",
            ClaimRepository.ClaimResult.Created =>
                $"✅ Claimed @{claim.InstagramUsername}. Shares from that account will now be attributed to you.",
            ClaimRepository.ClaimResult.Updated =>
                $"✅ Updated your claim to @{claim.InstagramUsername}.",
            _ => "Something went wrong recording your claim."
        };
    }

    private static async Task<string> HandleUnclaimAsync(DiscordUser user, ClaimRepository claims, CancellationToken ct)
    {
        var removed = await claims.UnclaimAsync(user.Id!, ct);
        return removed ? "✅ Your claim has been removed." : "You don't have a claim to remove.";
    }

    private static async Task<string> HandleWhoAmIAsync(DiscordUser user, ClaimRepository claims, CancellationToken ct)
    {
        var mine = await claims.GetByDiscordUserAsync(user.Id!, ct);
        if (mine is null)
        {
            return "You haven't claimed an Instagram username yet. Use `/claim username:<name>`.";
        }

        var linkStatus = mine.InstagramSenderId is null
            ? " (not yet linked to a message sender - it locks in the first time that account shares something)"
            : "";
        return $"You are claimed as @{mine.InstagramUsername}{linkStatus}.";
    }
}
