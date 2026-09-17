using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace DiscordMemeBot.Services;

/// <summary>Verifies the Ed25519 signature Discord attaches to every interaction HTTP request.</summary>
public static class DiscordSignatureVerifier
{
    public static bool Verify(ReadOnlySpan<byte> body, string? signatureHex, string? timestamp, string publicKeyHex)
    {
        if (string.IsNullOrEmpty(signatureHex) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(publicKeyHex))
        {
            return false;
        }

        try
        {
            var signature = Convert.FromHexString(signatureHex);
            var publicKeyBytes = Convert.FromHexString(publicKeyHex);
            var timestampBytes = Encoding.UTF8.GetBytes(timestamp);

            var message = new byte[timestampBytes.Length + body.Length];
            timestampBytes.CopyTo(message, 0);
            body.CopyTo(message.AsSpan(timestampBytes.Length));

            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            return SignatureAlgorithm.Ed25519.Verify(publicKey, message, signature);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
