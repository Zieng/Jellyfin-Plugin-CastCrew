using Jellyfin.Plugin.CastCrew.Configuration;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.CastCrew.Api;

public sealed class CastCrewActorsResponse
{
    public IReadOnlyList<BaseItemDto> Items { get; set; } = Array.Empty<BaseItemDto>();

    public int TotalRecordCount { get; set; }

    public int StartIndex { get; set; }

    public int PageSize { get; set; }

    public string SortBy { get; set; } = CastCrewConfigurationDefaults.SortByName;

    public string DetailRoutePreference { get; set; } = CastCrewConfigurationDefaults.RoutePreferenceAuto;

    /// <summary>
    /// Available tag filter options for the current tab scope.
    /// </summary>
    public IReadOnlyList<string> AvailableTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Available country/region filter options for the current tab scope.
    /// </summary>
    public IReadOnlyList<string> AvailableProductionLocations { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Items matched by name when a search term is active. Null when no search term.
    /// </summary>
    public IReadOnlyList<BaseItemDto>? NameMatchItems { get; set; }

    /// <summary>
    /// Count of persons whose name matched the search term.
    /// </summary>
    public int? NameMatchCount { get; set; }

    /// <summary>
    /// Items matched by description/overview when a search term is active. Null when no search term.
    /// </summary>
    public IReadOnlyList<BaseItemDto>? DescriptionMatchItems { get; set; }

    /// <summary>
    /// Count of persons whose description matched the search term.
    /// </summary>
    public int? DescriptionMatchCount { get; set; }

    /// <summary>
    /// Available library filter options for this response scope.
    /// </summary>
    public IReadOnlyList<CastCrewLibraryOption> AvailableLibraries { get; set; } = Array.Empty<CastCrewLibraryOption>();

    /// <summary>
    /// UTC timestamp of the latest completed person-to-library mapping build.
    /// Null when mapping has not completed yet.
    /// </summary>
    public DateTime? LibraryMappingLastSyncedUtc { get; set; }
}
