using Jellyfin.Plugin.CastCrew.Configuration;
using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

public sealed class CastCrewStartupSyncHostedService : IHostedService
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly CastCrewLibraryPersonMappingService _mappingService;
    private readonly ILogger<CastCrewStartupSyncHostedService> _logger;

    public CastCrewStartupSyncHostedService(
        IApplicationPaths applicationPaths,
        CastCrewLibraryPersonMappingService mappingService,
        ILogger<CastCrewStartupSyncHostedService> logger)
    {
        _applicationPaths = applicationPaths;
        _mappingService = mappingService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        _ = CastCrewWebConfigPatcher.SyncCastCrewMenuLink(
            _applicationPaths.WebPath,
            configuration.EnableCastCrewMainMenuEntry,
            _logger);

        // Build the person-to-library mapping in the background
        if (_mappingService is not null)
        {
            Task.Run(() =>
            {
                try
                {
                    _mappingService.RebuildMapping();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CastCrew] Failed to build person-to-library mapping on startup");
                }
            }, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
