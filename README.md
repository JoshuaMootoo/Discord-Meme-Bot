# Discord-Meme-Bot

Forwards shared Instagram/TikTok/X links from DMs to a Discord channel.

An ASP.NET Core (minimal API) service that receives Instagram DM webhook events
via the Meta Instagram Messaging API. When a post/reel is shared to the
connected Instagram account via DM (or a TikTok/X link is pasted into a DM),
the service forwards the link to a Discord channel via a webhook, attributing
it to whichever Discord user has `/claim`ed that Instagram account.

## Project layout

Single ASP.NET Core minimal API project at the repo root (no solution file
needed — `dotnet run` from the repo root is enough).

```
DiscordMemeBot.csproj
Program.cs                          # wiring only
Configuration/                      # strongly-typed options (InstagramOptions, DiscordOptions)
Models/                             # POCOs: webhook payloads, sender/claim storage, Discord interactions
Services/                           # signature verification, JSON file repos, Graph/Discord clients, processing pipeline
Endpoints/                          # minimal API route groups (webhook, senders, discord-interactions)
appsettings.json                    # committed, placeholder values only
appsettings.Development.json        # gitignored - your real local secrets go here
data/                               # gitignored JSON "database" files, created automatically on first run
  senders.json
  claims.json
```

`data/senders.json` and `data/claims.json` are created automatically the
first time they're needed — you don't need to create them yourself, and they
are gitignored so your real sender IDs/claims never get committed.

## Required configuration

All of these are empty placeholders in the committed `appsettings.json`. Fill
them in via **user-secrets** (recommended for local dev) or environment
variables — never commit real values.

| Key | Purpose |
|---|---|
| `Instagram:VerifyToken` | A string you invent yourself. Must match the "Verify token" field you type into the Meta App Dashboard's webhook config. |
| `Instagram:AccessToken` | The long-lived Instagram access token from the App Dashboard. Used to resolve sender IDs to usernames via the Graph API. |
| `Instagram:AppSecret` | The Instagram App Secret. Used to verify the `X-Hub-Signature-256` header on every webhook POST. |
| `Instagram:AllowedSenderIds` | Optional JSON array of Instagram-scoped sender IDs to pre-approve on first run (e.g. your own account, if you ever DM yourself for testing). Everyone else starts out `pending`. |
| `Discord:WebhookUrl` | The Discord channel webhook URL (Channel Settings → Integrations → Webhooks → New Webhook). |
| `Discord:PublicKey` | Your Discord Application's Public Key (Developer Portal → General Information). Used to verify interaction request signatures. |
| `Discord:ApplicationId` | Your Discord Application's ID. Needed to register the `/claim`, `/unclaim`, `/whoami` slash commands. |
| `Discord:BotToken` | Your Discord bot's token (Developer Portal → Bot). Needed to register slash commands. |
| `Discord:GuildId` | The Discord server (guild) ID to register the slash commands against, for instant availability. |

### Setting these locally with user-secrets

```bash
dotnet user-secrets init
dotnet user-secrets set "Instagram:VerifyToken" "some-string-you-invent"
dotnet user-secrets set "Instagram:AccessToken" "<your long-lived IG token>"
dotnet user-secrets set "Instagram:AppSecret" "<your IG app secret>"
dotnet user-secrets set "Discord:WebhookUrl" "<your discord channel webhook url>"
dotnet user-secrets set "Discord:PublicKey" "<your discord app public key>"
dotnet user-secrets set "Discord:ApplicationId" "<your discord application id>"
dotnet user-secrets set "Discord:BotToken" "<your discord bot token>"
dotnet user-secrets set "Discord:GuildId" "<your discord server id>"
```

Alternatively, create `appsettings.Development.json` (already gitignored)
with the same shape as `appsettings.json` but filled in, or set environment
variables using the `Instagram__AccessToken` (double-underscore) convention
when deploying.

## Running locally

```bash
dotnet run
```

By default this listens on `http://localhost:5181` (see
`Properties/launchSettings.json`). Slash command registration runs once at
startup and is skipped (with a log message, not an error) if
`ApplicationId`/`BotToken`/`GuildId` aren't all configured yet — so you can
iterate on the Instagram side without a Discord bot set up.

## Exposing it locally with ngrok

1. `dotnet run`
2. `ngrok http 5181` (match whatever port `dotnet run` printed)
3. Paste the resulting `https://xxxx.ngrok-free.app/webhook` URL into the Meta
   App Dashboard's webhook "Callback URL" field, along with the
   `Instagram:VerifyToken` value you configured.
4. Click "Verify and save" — this triggers the `GET /webhook` handshake.
5. Subscribe to the `messages` field for the Instagram account.
6. For the Discord side, paste `https://xxxx.ngrok-free.app/discord-interactions`
   into the Discord Developer Portal's "Interactions Endpoint URL" field
   (General Information page). Discord will send a PING immediately to
   verify it.
7. From another Instagram account, share a post to the connected account via
   DM (or paste a TikTok/X link) and confirm it shows up as `pending` (see
   below), then approve it and confirm the link shows up in Discord.

## Endpoints

### Instagram webhook

- `GET /webhook` — Meta's verification handshake. Echoes `hub.challenge`
  back as plain text if `hub.mode=subscribe` and `hub.verify_token` matches
  configuration; otherwise `403`.
- `POST /webhook` — receives DM events. Verifies `X-Hub-Signature-256`
  (HMAC-SHA256 of the raw body using `Instagram:AppSecret`) and rejects
  non-matching requests with `401`. Always returns `200` once the signature
  is valid, regardless of downstream Discord success/failure (Meta retries
  on non-200s, so failures are logged instead of surfaced here).

### Sender review

No auth on these — keep the service off the public internet, or add basic
auth in front of it (e.g. via a reverse proxy) if you deploy it somewhere
reachable by others.

- `GET /senders/pending` — pending senders awaiting a decision (ID,
  resolved username if available, last shared URL, first-seen time).
- `GET /senders/allowed` / `GET /senders/denied` — current allow/deny lists.
- `POST /senders/{senderId}/approve` — moves a sender from pending to
  allowed. `404` if the sender isn't currently pending or already allowed.
- `POST /senders/{senderId}/deny` — moves a sender from pending to denied.
  `404` if the sender isn't currently pending or already denied.

### Discord interactions

- `POST /discord-interactions` — Discord's HTTP Interactions endpoint.
  Verifies the `X-Signature-Ed25519` / `X-Signature-Timestamp` headers
  against `Discord:PublicKey` (via `NSec.Cryptography`) and rejects
  invalid requests with `401`. Responds to Discord's `PING` with `PONG`,
  and handles:
  - `/claim username:<instagram_username>` — claims that username for the
    calling Discord user. Rejected if already claimed by someone else.
  - `/unclaim` — removes the caller's own claim.
  - `/whoami` — shows the caller's current claim, if any.

  All command replies are ephemeral (only visible to the person who ran the
  command).

## How attribution works

- A share/link forwarded from an **unrecognized** sender doesn't get posted
  to Discord at all — it's resolved to a username (via the Graph API, cached
  for next time) and recorded as `pending` for review.
- Once a sender is **approved**, future shares get forwarded. If the
  resolved username matches a `/claim`, the Discord message shows
  `Shared by @DiscordUser (@ig_username): <url>` and the claim's sender ID
  gets locked in (so future matching relies on the stable ID, not the
  username, which could change). Otherwise it falls back to
  `Shared by @ig_username: <url>`, or just the raw link if the username
  couldn't be resolved.
- Each forwarded message is tagged with which platform it came from
  (📸 Instagram / 🎵 TikTok / 🐦 X).

**Known limitation:** claiming is honor-system only — there's no proof the
claiming Discord user actually controls the Instagram account. Fine for a
small friend-group bot; anyone could squat an unclaimed username. See the
spec's "Known limitation" note if this ever needs tightening (e.g. requiring
an admin to assign the mapping after the pending-review approval).

## Logging

Console logging is on by default (`Information` level). Received Instagram
webhook payloads and any parsing/posting errors are logged — useful while
you're still figuring out the exact shape of payloads your account sends.
The verify token, app secret, bot token, and public key are never logged.
