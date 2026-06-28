using Jellyfin.Plugin.CastCrew.Configuration;
using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

public sealed class CastCrewStartupSyncHostedService : IHostedService
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILibraryManager _libraryManager;
    private readonly CastCrewLibraryPersonMappingService _mappingService;
    private readonly ILogger<CastCrewStartupSyncHostedService> _logger;
    private bool _libraryEventsRegistered;

    public CastCrewStartupSyncHostedService(
        IApplicationPaths applicationPaths,
        ILibraryManager libraryManager,
        CastCrewLibraryPersonMappingService mappingService,
        ILogger<CastCrewStartupSyncHostedService> logger)
    {
        _applicationPaths = applicationPaths;
        _libraryManager = libraryManager;
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

        if (!_libraryEventsRegistered)
        {
            _libraryManager.ItemAdded += OnLibraryItemChanged;
            _libraryManager.ItemUpdated += OnLibraryItemChanged;
            _libraryManager.ItemRemoved += OnLibraryItemChanged;
            _libraryEventsRegistered = true;
        }

        _mappingService.QueueRebuild("plugin startup", TimeSpan.Zero);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_libraryEventsRegistered)
        {
            _libraryManager.ItemAdded -= OnLibraryItemChanged;
            _libraryManager.ItemUpdated -= OnLibraryItemChanged;
            _libraryManager.ItemRemoved -= OnLibraryItemChanged;
            _libraryEventsRegistered = false;
        }

        _mappingService.CancelPendingRebuild();

        return Task.CompletedTask;
    }

    private void OnLibraryItemChanged(object? sender, ItemChangeEventArgs eventArgs)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration;
        if (configuration?.IncludedLibraryIds is null || configuration.IncludedLibraryIds.Length == 0)
        {
            return;
        }

        var updateReason = eventArgs.UpdateReason.ToString();
        _mappingService.QueueRebuild("library item changed: " + updateReason);
    }
}
