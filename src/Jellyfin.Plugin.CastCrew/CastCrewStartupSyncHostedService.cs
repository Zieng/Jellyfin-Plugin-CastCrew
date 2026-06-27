using Jellyfin.Plugin.CastCrew.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

public sealed class CastCrewStartupSyncHostedService : IHostedService
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<CastCrewStartupSyncHostedService> _logger;

    public CastCrewStartupSyncHostedService(
        IApplicationPaths applicationPaths,
        ILogger<CastCrewStartupSyncHostedService> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        _ = CastCrewWebConfigPatcher.SyncCastCrewMenuLink(
            _applicationPaths.WebPath,
            configuration.EnableCastCrewMainMenuEntry,
            _logger);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
