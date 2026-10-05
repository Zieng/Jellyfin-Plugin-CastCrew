using Jellyfin.Plugin.CastCrew.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Metadata;

public sealed class MasexMetadataProvider : HtmlMetadataProvider
{
    public MasexMetadataProvider(ILogger<MasexMetadataProvider> logger) : base(logger) { }
    public override string Name => "MASex";
    protected override string BaseUrl => CastCrewPlugin.Instance?.Configuration.MasexActorUrl ?? "https://masex.tv/zh-CN/actor";
    protected override bool Enabled(PluginConfiguration configuration) => configuration.EnableMasexProvider;
}
