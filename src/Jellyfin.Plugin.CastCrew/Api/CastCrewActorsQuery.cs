namespace Jellyfin.Plugin.CastCrew.Api;

public sealed class CastCrewActorsQuery
{
    public int? StartIndex { get; set; }

    public int? Limit { get; set; }

    public string? SearchTerm { get; set; }

    public string? SortBy { get; set; }

    public string? SortOrder { get; set; }

    public bool? IsFavorite { get; set; }

    public string? Tag { get; set; }

    public string? ProductionLocation { get; set; }

    public string? LibraryIds { get; set; }

    public Guid? UserId { get; set; }
}
