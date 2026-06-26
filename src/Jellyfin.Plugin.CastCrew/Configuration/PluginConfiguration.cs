using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CastCrew.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int DefaultPageSize { get; set; } = CastCrewConfigurationDefaults.DefaultPageSize;

    public string DefaultSortBy { get; set; } = CastCrewConfigurationDefaults.SortByName;

    public bool EnableCastCrewMainMenuEntry { get; set; } = true;

    public string DetailRoutePreference { get; set; } = CastCrewConfigurationDefaults.RoutePreferenceAuto;
}

public static class CastCrewConfigurationDefaults
{
    public const int DefaultPageSize = 50;
    public const int MinPageSize = 10;
    public const int MaxPageSize = 200;

    public const string SortByName = "Name";
    public const string SortByDateCreated = "DateCreated";
    public const string SortByRandom = "Random";

    public const string SortOrderAscending = "Ascending";
    public const string SortOrderDescending = "Descending";

    public const string RoutePreferenceAuto = "Auto";
    public const string RoutePreferenceHashBang = "HashBang";
    public const string RoutePreferenceHash = "Hash";
}
