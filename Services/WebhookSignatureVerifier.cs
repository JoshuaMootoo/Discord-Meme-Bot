using System.Security.Cryptography;
using System.Text;

namespace DiscordMemeBot.Services;

/// <summary>Verifies Meta's X-Hub-Signature-256 header on Instagram webhook POSTs.</summary>
public static class WebhookSignatureVerifier
{
    private const string Prefix = "sha256=";

    public static bool Verify(ReadOnlySpan<byte> payload, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(appSecret))
        {
            return false;
        }

        if (!signatureHeader.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(signatureHeader.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var actual = hmac.ComputeHash(payload.ToArray());

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
