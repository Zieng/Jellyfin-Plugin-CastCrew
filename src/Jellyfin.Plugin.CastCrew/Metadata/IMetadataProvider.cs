namespace Jellyfin.Plugin.CastCrew.Metadata;

public interface IMetadataProvider
{
    string Name { get; }
    Task<IReadOnlyList<MetadataPerson>> SearchAsync(MetadataSearchQuery query, CancellationToken cancellationToken);
}
