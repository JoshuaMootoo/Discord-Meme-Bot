using System.Text.RegularExpressions;
using DiscordMemeBot.Models;

namespace DiscordMemeBot.Services;

/// <summary>Scans free-form DM text for TikTok/X/Twitter links (see spec's "Multi-source link detection").</summary>
public static partial class LinkExtractor
{
    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    public static IEnumerable<SharedLink> ExtractLinks(string text)
    {
        foreach (Match match in UrlPattern().Matches(text))
        {
            var url = match.Value.TrimEnd('.', ',', ')', ']', '"', '\'');

            string host;
            try
            {
                host = new Uri(url).Host.ToLowerInvariant();
            }
            catch (UriFormatException)
            {
                continue;
            }

            if (host.EndsWith("tiktok.com", StringComparison.Ordinal))
            {
                yield return new SharedLink(url, LinkSource.TikTok);
            }
            else if (host.EndsWith("x.com", StringComparison.Ordinal) || host.EndsWith("twitter.com", StringComparison.Ordinal))
            {
                yield return new SharedLink(url, LinkSource.X);
            }
        }
    }
}
