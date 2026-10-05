using Jellyfin.Plugin.CastCrew.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Metadata;

public sealed class MetadataSearchService
{
    private readonly IReadOnlyList<IMetadataProvider> _providers;
    private readonly ILogger<MetadataSearchService> _logger;

    public MetadataSearchService(IEnumerable<IMetadataProvider> providers, ILogger<MetadataSearchService> logger)
    {
        _providers = providers.ToArray();
        _logger = logger;
    }

    public async Task<MetadataSearchResponse> SearchAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        var enabled = _providers.Where(p => IsEnabled(p, configuration)).ToArray();
        var errors = new List<string>();
        var all = new List<MetadataPerson>();
        foreach (var provider in enabled)
        {
            try
            {
                all.AddRange(await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CastCrew] Metadata provider {Provider} failed.", provider.Name);
                errors.Add($"{provider.Name}: {ex.Message}");
            }
        }

        var deduped = all
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => NormalizeKey(p.Name))
            .Select(g => Merge(g))
            .Where(p => Matches(p, query))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var start = Math.Max(0, query.StartIndex);
        var limit = Math.Clamp(query.Limit, 1, 200);
        return new MetadataSearchResponse
        {
            Items = deduped.Skip(start).Take(limit).ToArray(),
            TotalRecordCount = deduped.Length,
            EnabledSources = enabled.Select(p => p.Name).ToArray(),
            Errors = errors
        };
    }

    private static bool IsEnabled(IMetadataProvider provider, PluginConfiguration c) => provider.Name switch
    {
        "TMDB" => c.EnableTmdbProvider,
        "MASex" => c.EnableMasexProvider,
        "Netflav" => c.EnableNetflavProvider,
        _ => false
    };

    private static string NormalizeKey(string value) => value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static MetadataPerson Merge(IEnumerable<MetadataPerson> values)
    {
        var first = values.First();
        foreach (var value in values.Skip(1))
        {
            first.ProfileUrl ??= value.ProfileUrl;
            first.ImageUrl ??= value.ImageUrl;
            first.Gender ??= value.Gender;
            first.Age ??= value.Age;
            first.HeightCm ??= value.HeightCm;
            first.BustCm ??= value.BustCm;
            first.WaistCm ??= value.WaistCm;
            first.HipCm ??= value.HipCm;
            first.VideoCount ??= value.VideoCount;
            first.Country ??= value.Country;
            first.Overview ??= value.Overview;
            first.ExternalId ??= value.ExternalId;
            if (!first.Source.Contains(value.Source, StringComparison.OrdinalIgnoreCase)) first.Source += "," + value.Source;
        }
        return first;
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
