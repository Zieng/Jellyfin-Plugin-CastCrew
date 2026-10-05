namespace Jellyfin.Plugin.CastCrew.Metadata;

public enum MetadataPersonType
{
    Actor,
    Director,
    Studio
}

public sealed class MetadataPerson
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BustCm { get; set; }
    public decimal? WaistCm { get; set; }
    public decimal? HipCm { get; set; }
    public int? VideoCount { get; set; }
    public string? Country { get; set; }
    public string? Overview { get; set; }
    public string? ExternalId { get; set; }
}

public sealed class MetadataSearchQuery
{
    public MetadataPersonType Type { get; set; } = MetadataPersonType.Actor;
    public string? Name { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? MinHeight { get; set; }
    public decimal? MaxHeight { get; set; }
    public decimal? MinBust { get; set; }
    public decimal? MaxBust { get; set; }
    public decimal? MinWaist { get; set; }
    public decimal? MaxWaist { get; set; }
    public decimal? MinHip { get; set; }
    public decimal? MaxHip { get; set; }
    public int? MinVideoCount { get; set; }
    public int? MaxVideoCount { get; set; }
    public int StartIndex { get; set; }
    public int Limit { get; set; } = 50;
}

public sealed class MetadataSearchResponse
{
    public IReadOnlyList<MetadataPerson> Items { get; init; } = Array.Empty<MetadataPerson>();
    public int TotalRecordCount { get; init; }
    public IReadOnlyList<string> EnabledSources { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
