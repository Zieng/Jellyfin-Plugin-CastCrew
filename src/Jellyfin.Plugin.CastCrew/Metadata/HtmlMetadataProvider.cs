using System.Net;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CastCrew.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Metadata;

/// <summary>Small dependency-free HTML provider used for sites without a public JSON API.</summary>
public abstract class HtmlMetadataProvider : IMetadataProvider
{
    private static readonly Regex LinkRegex = new("<a[^>]+href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected HtmlMetadataProvider(ILogger logger)
    {
        Logger = logger;
        HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Jellyfin-CastCrew/2.0");
    }

    public abstract string Name { get; }
    protected abstract string BaseUrl { get; }
    protected abstract bool Enabled(PluginConfiguration configuration);

    public async Task<IReadOnlyList<MetadataPerson>> SearchAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!Enabled(configuration) || query.Type != MetadataPersonType.Actor)
            return Array.Empty<MetadataPerson>();

        var url = BuildSearchUrl(configuration, query);
        var html = await HttpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return ParseListing(html, url, query).Take(Math.Max(100, query.Limit * 5)).ToArray();
    }

    protected virtual string BuildSearchUrl(PluginConfiguration configuration, MetadataSearchQuery query)
    {
        var url = BaseUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(query.Name))
            url += "?search=" + Uri.EscapeDataString(query.Name.Trim());
        return url;
    }

    protected virtual IEnumerable<MetadataPerson> ParseListing(string html, string pageUrl, MetadataSearchQuery query)
    {
        foreach (Match match in LinkRegex.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            var text = WebUtility.HtmlDecode(TagRegex.Replace(match.Groups["text"].Value, " "));
            text = Regex.Replace(text, "\\s+", " ").Trim();
            if (text.Length < 2 || href.Length < 2) continue;
            if (!href.Contains("actor", StringComparison.OrdinalIgnoreCase) && !href.Contains("actress", StringComparison.OrdinalIgnoreCase)) continue;
            if (text.Equals("actor", StringComparison.OrdinalIgnoreCase) || text.Equals("actress", StringComparison.OrdinalIgnoreCase)) continue;
            yield return new MetadataPerson
            {
                Id = $"{Name}:{href}", Source = Name, Name = text,
                ProfileUrl = ToAbsoluteUrl(pageUrl, href)
            };
        }
    }

    protected static string ToAbsoluteUrl(string pageUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)) return absolute.ToString();
        if (Uri.TryCreate(new Uri(pageUrl), href, out var relative)) return relative.ToString();
        return href;
    }
}
