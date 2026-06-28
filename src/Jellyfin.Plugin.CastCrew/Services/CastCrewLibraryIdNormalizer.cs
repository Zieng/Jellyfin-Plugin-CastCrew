namespace Jellyfin.Plugin.CastCrew.Services;

internal static class CastCrewLibraryIdNormalizer
{
    internal static string NormalizeLibraryId(string? libraryId)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return string.Empty;
        }

        var trimmed = libraryId.Trim();
        if (Guid.TryParse(trimmed, out var parsedId))
        {
            return parsedId.ToString("N");
        }

        return trimmed;
    }
}
