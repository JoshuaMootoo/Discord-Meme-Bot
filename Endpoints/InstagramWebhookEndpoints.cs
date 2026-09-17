using System.Text.Json;
using DiscordMemeBot.Configuration;
using DiscordMemeBot.Models;
using DiscordMemeBot.Services;
using Microsoft.Extensions.Options;

namespace DiscordMemeBot.Endpoints;

public static class InstagramWebhookEndpoints
{
    public static void MapInstagramWebhookEndpoints(this WebApplication app)
    {
        app.MapGet("/webhook", (HttpRequest request, IOptions<InstagramOptions> options, ILogger<Program> logger) =>
        {
            var mode = request.Query["hub.mode"].ToString();
            var token = request.Query["hub.verify_token"].ToString();
            var challenge = request.Query["hub.challenge"].ToString();

            var verifyToken = options.Value.VerifyToken;
            if (mode == "subscribe" && !string.IsNullOrEmpty(verifyToken) && token == verifyToken)
            {
                logger.LogInformation("Instagram webhook verification succeeded.");
                return Results.Text(challenge, "text/plain");
            }

            logger.LogWarning("Instagram webhook verification failed (hub.mode={Mode}).", mode);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        app.MapPost("/webhook", async (
            HttpContext context,
            InstagramShareProcessor processor,
            IOptions<InstagramOptions> options,
            ILogger<Program> logger) =>
        {
            using var bodyStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(bodyStream, context.RequestAborted);
            var bodyBytes = bodyStream.ToArray();

            var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
            if (!WebhookSignatureVerifier.Verify(bodyBytes, signature, options.Value.AppSecret))
            {
                logger.LogWarning("Rejected Instagram webhook POST with missing/invalid X-Hub-Signature-256.");
                return Results.Unauthorized();
            }

            logger.LogInformation("Received Instagram webhook payload: {Payload}", System.Text.Encoding.UTF8.GetString(bodyBytes));

            InstagramWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<InstagramWebhookPayload>(bodyBytes, JsonFileStore.Options);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse Instagram webhook payload.");
                return Results.Ok();
            }

            if (payload is not null)
            {
                try
                {
                    await processor.ProcessAsync(payload, context.RequestAborted);
                }
                catch (Exception ex)
                {
                    // Never fail the webhook response over a downstream error - Meta retries on non-200s.
                    logger.LogError(ex, "Error processing Instagram webhook payload.");
                }
            }

            return Results.Ok();
        });
    }
}
