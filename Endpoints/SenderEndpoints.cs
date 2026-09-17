using DiscordMemeBot.Services;

namespace DiscordMemeBot.Endpoints;

public static class SenderEndpoints
{
    public static void MapSenderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/senders");

        group.MapGet("/pending", async (SenderRepository senders, CancellationToken ct) =>
            Results.Ok(await senders.GetPendingAsync(ct)));

        group.MapGet("/allowed", async (SenderRepository senders, CancellationToken ct) =>
            Results.Ok(await senders.GetAllowedAsync(ct)));

        group.MapGet("/denied", async (SenderRepository senders, CancellationToken ct) =>
            Results.Ok(await senders.GetDeniedAsync(ct)));

        group.MapPost("/{senderId}/approve", async (string senderId, SenderRepository senders, CancellationToken ct) =>
        {
            var found = await senders.ApproveAsync(senderId, ct);
            return found ? Results.Ok(new { senderId, status = "allowed" }) : Results.NotFound();
        });

        group.MapPost("/{senderId}/deny", async (string senderId, SenderRepository senders, CancellationToken ct) =>
        {
            var found = await senders.DenyAsync(senderId, ct);
            return found ? Results.Ok(new { senderId, status = "denied" }) : Results.NotFound();
        });
    }
}
