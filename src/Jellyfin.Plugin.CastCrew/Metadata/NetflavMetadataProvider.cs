using System.Net;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CastCrew.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Metadata;

public sealed class NetflavMetadataProvider : HtmlMetadataProvider
{
    private static readonly Regex MetricRegex = new("(?<label>age|height|胸围|bust|腰围|waist|臀围|hip|videos?|影片)\\D{0,15}(?<value>\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public NetflavMetadataProvider(ILogger<NetflavMetadataProvider> logger) : base(logger) { }
    public override string Name => "Netflav";
    protected override string BaseUrl => CastCrewPlugin.Instance?.Configuration.NetflavActorUrl ?? "https://netflav.com/actress";
    protected override bool Enabled(PluginConfiguration configuration) => configuration.EnableNetflavProvider;

    protected override IEnumerable<MetadataPerson> ParseListing(string html, string pageUrl, MetadataSearchQuery query)
    {
        foreach (var person in base.ParseListing(html, pageUrl, query))
        {
            if (!string.IsNullOrWhiteSpace(query.Name) && !person.Name.Contains(query.Name.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            yield return person;
        }
    }

    public override async Task<IReadOnlyList<MetadataPerson>> SearchAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        var results = await base.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        // Detail pages are fetched lazily only when filters need attributes; this keeps broad searches cheap.
        if (query.MinAge is null && query.MaxAge is null && query.MinHeight is null && query.MaxHeight is null &&
            query.MinBust is null && query.MaxBust is null && query.MinWaist is null && query.MaxWaist is null &&
            query.MinHip is null && query.MaxHip is null && query.MinVideoCount is null && query.MaxVideoCount is null)
            return results;

        var filtered = new List<MetadataPerson>();
        foreach (var person in results)
        {
            if (string.IsNullOrWhiteSpace(person.ProfileUrl)) continue;
            try
            {
                var html = await HttpClient.GetStringAsync(person.ProfileUrl, cancellationToken).ConfigureAwait(false);
                ParseMetrics(html, person);
                if (Matches(person, query)) filtered.Add(person);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "[CastCrew] Netflav profile fetch failed for {ProfileUrl}.", person.ProfileUrl);
            }
        }
        return filtered;
    }

    private static void ParseMetrics(string html, MetadataPerson person)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        text = Regex.Replace(text, "\\s+", " ");
        foreach (Match match in MetricRegex.Matches(text))
        {
            if (!decimal.TryParse(match.Groups["value"].Value, out var value)) continue;
            var label = match.Groups["label"].Value.ToLowerInvariant();
            if (label == "age" && value <= 120) person.Age = (int)value;
            else if (label == "height" && value is > 100 and < 230) person.HeightCm = value;
            else if (label is "胸围" or "bust") person.BustCm = value;
            else if (label is "腰围" or "waist") person.WaistCm = value;
            else if (label is "臀围" or "hip") person.HipCm = value;
            else if (label.Contains("video") || label.Contains("影片")) person.VideoCount = (int)value;
        }
    }

    private static bool Matches(MetadataPerson p, MetadataSearchQuery q) =>
        InRange(p.Age, q.MinAge, q.MaxAge) && InRange(p.HeightCm, q.MinHeight, q.MaxHeight) &&
        InRange(p.BustCm, q.MinBust, q.MaxBust) && InRange(p.WaistCm, q.MinWaist, q.MaxWaist) &&
        InRange(p.HipCm, q.MinHip, q.MaxHip) && InRange(p.VideoCount, q.MinVideoCount, q.MaxVideoCount);

    private static bool InRange<T>(T? value, T? min, T? max) where T : struct, IComparable<T>
    {
        if (min is null && max is null) return true;
        if (value is null) return false;
        if (min is not null && value.Value.CompareTo(min.Value) < 0) return false;
        if (max is not null && value.Value.CompareTo(max.Value) > 0) return false;
        return true;
    }
}
