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
        CastCrewDebugLogging.LogInformation(
            _logger,
            "Startup sync beginning. MainMenuEntryEnabled={MainMenuEntryEnabled}, IncludedLibraryCount={IncludedLibraryCount}.",
            configuration.EnableCastCrewMainMenuEntry,
            configuration.IncludedLibraryIds?.Length ?? 0);

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
            CastCrewDebugLogging.LogInformation(_logger, "Registered CastCrew library change listeners.");
        }

        _mappingService.QueueRebuild("plugin startup", TimeSpan.Zero);
        CastCrewDebugLogging.LogInformation(_logger, "Queued initial mapping rebuild on startup.");

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
            CastCrewDebugLogging.LogInformation(_logger, "Unregistered CastCrew library change listeners.");
        }

        _mappingService.CancelPendingRebuild();
        CastCrewDebugLogging.LogInformation(_logger, "Cancelled pending mapping rebuild requests on shutdown.");

        return Task.CompletedTask;
    }

    private void OnLibraryItemChanged(object? sender, ItemChangeEventArgs eventArgs)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration;
        if (configuration?.IncludedLibraryIds is null || configuration.IncludedLibraryIds.Length == 0)
        {
            CastCrewDebugLogging.LogInformation(
                _logger,
                "Ignoring library change trigger because IncludedLibraryIds is empty (all libraries mode).");
            return;
        }

        var updateReason = eventArgs.UpdateReason.ToString();
        CastCrewDebugLogging.LogInformation(_logger, "Library item changed. UpdateReason={UpdateReason}.", updateReason);
        _mappingService.QueueRebuild("library item changed: " + updateReason);
    }
}
