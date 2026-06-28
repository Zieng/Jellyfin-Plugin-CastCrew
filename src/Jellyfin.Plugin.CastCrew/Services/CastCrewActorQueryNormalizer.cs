using Jellyfin.Plugin.CastCrew.Api;
using Jellyfin.Plugin.CastCrew.Configuration;

namespace Jellyfin.Plugin.CastCrew.Services;

public static class CastCrewActorQueryNormalizer
{
    public static NormalizedCastCrewActorQuery Normalize(CastCrewActorsQuery query, PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(configuration);

        var defaultPageSize = ClampPageSize(configuration.DefaultPageSize);
        var requestedLimit = query.Limit.GetValueOrDefault(defaultPageSize);
        var limit = requestedLimit <= 0 ? defaultPageSize : ClampPageSize(requestedLimit);

        var startIndex = Math.Max(query.StartIndex.GetValueOrDefault(0), 0);

        return new NormalizedCastCrewActorQuery
        {
            StartIndex = startIndex,
            Limit = limit,
            SearchTerm = NormalizeSearchTerm(query.SearchTerm),
            Tag = NormalizeFacetValue(query.Tag),
            ProductionLocation = NormalizeFacetValue(query.ProductionLocation),
            RequestedLibraryIds = NormalizeLibraryIds(query.LibraryIds),
            SortBy = NormalizeSortBy(query.SortBy, configuration.DefaultSortBy),
            SortOrder = NormalizeSortOrder(query.SortOrder),
            IsFavorite = query.IsFavorite,
            DetailRoutePreference = NormalizeDetailRoutePreference(configuration.DetailRoutePreference)
        };
    }

    public static string NormalizeSortBy(string? requestedSortBy, string? fallbackSortBy)
    {
        if (TryNormalizeSortBy(requestedSortBy, out var normalizedSort))
        {
            return normalizedSort;
        }

        if (TryNormalizeSortBy(fallbackSortBy, out normalizedSort))
        {
            return normalizedSort;
        }

        return CastCrewConfigurationDefaults.SortByName;
    }

    public static string NormalizeDetailRoutePreference(string? value)
    {
        if (string.Equals(value, CastCrewConfigurationDefaults.RoutePreferenceHashBang, StringComparison.OrdinalIgnoreCase))
        {
            return CastCrewConfigurationDefaults.RoutePreferenceHashBang;
        }

        if (string.Equals(value, CastCrewConfigurationDefaults.RoutePreferenceHash, StringComparison.OrdinalIgnoreCase))
        {
            return CastCrewConfigurationDefaults.RoutePreferenceHash;
        }

        return CastCrewConfigurationDefaults.RoutePreferenceAuto;
    }

    private static int ClampPageSize(int value)
    {
        if (value < CastCrewConfigurationDefaults.MinPageSize)
        {
            return CastCrewConfigurationDefaults.MinPageSize;
        }

        if (value > CastCrewConfigurationDefaults.MaxPageSize)
        {
            return CastCrewConfigurationDefaults.MaxPageSize;
        }

        return value;
    }

    private static string? NormalizeSearchTerm(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        return searchTerm.Trim();
    }

    private static string? NormalizeFacetValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeLibraryIds(string? rawLibraryIds)
    {
        if (string.IsNullOrWhiteSpace(rawLibraryIds))
        {
            return Array.Empty<string>();
        }

        return rawLibraryIds
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(CastCrewLibraryIdNormalizer.NormalizeLibraryId)
            .Where(libraryId => libraryId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryNormalizeSortBy(string? value, out string normalizedSortBy)
    {
        if (string.Equals(value, CastCrewConfigurationDefaults.SortByDateCreated, StringComparison.OrdinalIgnoreCase))
        {
            normalizedSortBy = CastCrewConfigurationDefaults.SortByDateCreated;
            return true;
        }

        if (string.Equals(value, CastCrewConfigurationDefaults.SortByName, StringComparison.OrdinalIgnoreCase))
        {
            normalizedSortBy = CastCrewConfigurationDefaults.SortByName;
            return true;
        }

        if (string.Equals(value, CastCrewConfigurationDefaults.SortByRandom, StringComparison.OrdinalIgnoreCase))
        {
            normalizedSortBy = CastCrewConfigurationDefaults.SortByRandom;
            return true;
        }

        normalizedSortBy = string.Empty;
        return false;
    }

    public static string NormalizeSortOrder(string? value)
    {
        if (string.Equals(value, CastCrewConfigurationDefaults.SortOrderDescending, StringComparison.OrdinalIgnoreCase))
        {
            return CastCrewConfigurationDefaults.SortOrderDescending;
        }

        return CastCrewConfigurationDefaults.SortOrderAscending;
    }
}

public sealed class NormalizedCastCrewActorQuery
{
    public required int StartIndex { get; init; }

    public required int Limit { get; init; }

    public required string SortBy { get; init; }

    public required string SortOrder { get; init; }

    public bool? IsFavorite { get; init; }

    public string? SearchTerm { get; init; }

    public string? Tag { get; init; }

    public string? ProductionLocation { get; init; }

    public IReadOnlyList<string> RequestedLibraryIds { get; init; } = Array.Empty<string>();

    public required string DetailRoutePreference { get; init; }
}
