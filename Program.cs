using DiscordMemeBot.Configuration;
using DiscordMemeBot.Endpoints;
using DiscordMemeBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<InstagramOptions>(builder.Configuration.GetSection("Instagram"));
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection("Discord"));

builder.Services.AddHttpClient<InstagramGraphClient>();
builder.Services.AddHttpClient<DiscordNotifier>();
builder.Services.AddHttpClient(nameof(DiscordCommandRegistrationService));

builder.Services.AddSingleton<SenderRepository>();
builder.Services.AddSingleton<ClaimRepository>();
builder.Services.AddSingleton<InstagramShareProcessor>();
builder.Services.AddHostedService<DiscordCommandRegistrationService>();

var app = builder.Build();

app.MapInstagramWebhookEndpoints();
app.MapSenderEndpoints();
app.MapDiscordInteractionEndpoints();

app.Run();
