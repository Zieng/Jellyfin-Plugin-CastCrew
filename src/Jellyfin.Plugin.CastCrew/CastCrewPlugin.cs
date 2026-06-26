using Jellyfin.Plugin.CastCrew.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CastCrew;

public class CastCrewPlugin : BasePlugin<PluginConfiguration>, IHasPluginConfiguration, IHasWebPages
{
    public static CastCrewPlugin? Instance { get; private set; }
    private readonly IApplicationPaths _applicationPaths;

    public CastCrewPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        _applicationPaths = applicationPaths;
        Instance = this;

        // Deferred sync: catch and log failures instead of crashing plugin load (issue #12)
        try
        {
            CastCrewWebConfigPatcher.SyncCastCrewMenuLink(
                _applicationPaths.WebPath,
                Configuration?.EnableCastCrewMainMenuEntry ?? true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CastCrew] Failed to sync menu link on startup: {ex.Message}");
        }
    }

    public override Guid Id => Guid.Parse("a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a");

    public override string Name => "CastCrew";

    public override string Description => "Adds a Cast & Crew module to the Jellyfin sidebar for browsing actors, directors, producers, and other crew members.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var prefix = GetType().Namespace;
        var configuration = Configuration ?? new PluginConfiguration();
        CastCrewWebConfigPatcher.SyncCastCrewMenuLink(_applicationPaths.WebPath, configuration.EnableCastCrewMainMenuEntry);

        yield return new PluginPageInfo
        {
            Name = "castcrew-config",
            DisplayName = "CastCrew",
            EmbeddedResourcePath = $"{prefix}.Configuration.config.html"
        };
    }
}
