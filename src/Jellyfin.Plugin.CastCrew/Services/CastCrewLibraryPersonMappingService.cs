using System.Collections;
using Jellyfin.Plugin.CastCrew.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Services;

/// <summary>
/// Maintains a cached mapping of person names to the library IDs they appear in.
/// This allows filtering Cast &amp; Crew results by media library.
/// </summary>
public sealed class CastCrewLibraryPersonMappingService
{
    private static readonly TimeSpan DefaultRebuildDebounceDelay = TimeSpan.FromSeconds(3);

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<CastCrewLibraryPersonMappingService> _logger;
    private readonly object _lock = new();
    private readonly object _rebuildLock = new();
    private readonly object _rebuildScheduleLock = new();

    // personName (case-insensitive key) → set of library ItemIds (as strings)
    private Dictionary<string, HashSet<string>> _personLibraryMap = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastBuildTime = DateTime.MinValue;
    private CancellationTokenSource? _scheduledRebuildCancellation;

    public CastCrewLibraryPersonMappingService(ILibraryManager libraryManager, IUserManager userManager, ILogger<CastCrewLibraryPersonMappingService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the time the mapping was last built.
    /// </summary>
    public DateTime LastBuildTime
    {
        get { lock (_lock) { return _lastBuildTime; } }
    }

    /// <summary>
    /// Rebuilds the person-to-library mapping by scanning all libraries.
    /// </summary>
    public void RebuildMapping()
    {
        lock (_rebuildLock)
        {
            _logger.LogInformation("[CastCrew] Building person-to-library mapping...");

            var newMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var virtualFolders = _libraryManager.GetVirtualFolders();
                var libraryIdSet = new HashSet<string>(
                    virtualFolders
                        .Select(f => NormalizeLibraryId(f.ItemId))
                        .Where(id => id.Length > 0),
                    StringComparer.OrdinalIgnoreCase);

                if (libraryIdSet.Count == 0)
                {
                    _logger.LogInformation("[CastCrew] No libraries found, mapping is empty");
                    lock (_lock)
                    {
                        _personLibraryMap = newMap;
                        _lastBuildTime = DateTime.UtcNow;
                    }

                    return;
                }

                // Query using a concrete user context for broad Jellyfin runtime compatibility.
                var queryUser = GetAnyQueryUser();
                if (queryUser is null)
                {
                    _logger.LogWarning("[CastCrew] No users found, cannot build person-library mapping yet");
                    return;
                }

                var allItems = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    User = queryUser,
                    Recursive = true,
                    IsVirtualItem = false,
                    IsMovie = true
                });

                _logger.LogInformation("[CastCrew] Found {Count} movie items to process for library mapping", allItems.Count);

                foreach (var item in allItems)
                {
                    var people = _libraryManager.GetPeople(item);
                    if (people is null || people.Count == 0)
                    {
                        continue;
                    }

                    // Determine which libraries this item belongs to
                    var collectionFolders = _libraryManager.GetCollectionFolders(item);
                    var itemLibraryIds = new List<string>();
                    foreach (var folder in collectionFolders)
                    {
                        var folderId = NormalizeLibraryId(folder.Id.ToString("N"));
                        if (folderId.Length > 0 && libraryIdSet.Contains(folderId))
                        {
                            itemLibraryIds.Add(folderId);
                        }
                    }

                    if (itemLibraryIds.Count == 0)
                    {
                        continue;
                    }

                    foreach (var personInfo in people)
                    {
                        if (string.IsNullOrWhiteSpace(personInfo.Name))
                        {
                            continue;
                        }

                        var normalizedPersonName = personInfo.Name.Trim();
                        if (normalizedPersonName.Length == 0)
                        {
                            continue;
                        }

                        if (!newMap.TryGetValue(normalizedPersonName, out var personLibraries))
                        {
                            personLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            newMap[normalizedPersonName] = personLibraries;
                        }

                        foreach (var libId in itemLibraryIds)
                        {
                            personLibraries.Add(libId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CastCrew] Failed to build person-to-library mapping");
                return;
            }

            lock (_lock)
            {
                _personLibraryMap = newMap;
                _lastBuildTime = DateTime.UtcNow;
            }

            _logger.LogInformation("[CastCrew] Person-to-library mapping built: {Count} persons mapped", newMap.Count);
        }
    }

    /// <summary>
    /// Ensures the mapping has been built at least once.
    /// </summary>
    public void EnsureMappingBuilt()
    {
        if (LastBuildTime != DateTime.MinValue)
        {
            return;
        }

        RebuildMapping();
    }

    /// <summary>
    /// Schedules a mapping rebuild with debounce to avoid repeated rebuild storms during scans.
    /// </summary>
    /// <param name="reason">Trigger reason for logging.</param>
    /// <param name="debounceDelay">Optional debounce delay.</param>
    public void QueueRebuild(string reason, TimeSpan? debounceDelay = null)
    {
        var delay = debounceDelay.GetValueOrDefault(DefaultRebuildDebounceDelay);

        CancellationTokenSource scheduledCts;
        lock (_rebuildScheduleLock)
        {
            _scheduledRebuildCancellation?.Cancel();
            _scheduledRebuildCancellation?.Dispose();

            _scheduledRebuildCancellation = new CancellationTokenSource();
            scheduledCts = _scheduledRebuildCancellation;
        }

        _logger.LogDebug(
            "[CastCrew] Scheduled person-to-library mapping rebuild in {DelayMs} ms (reason: {Reason})",
            Math.Max((int)delay.TotalMilliseconds, 0),
            reason);

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, scheduledCts.Token).ConfigureAwait(false);
                }

                RebuildMapping();
            }
            catch (OperationCanceledException) when (scheduledCts.IsCancellationRequested)
            {
                // Debounced by a newer request.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CastCrew] Scheduled person-to-library mapping rebuild failed (reason: {Reason})", reason);
            }
            finally
            {
                lock (_rebuildScheduleLock)
                {
                    if (ReferenceEquals(_scheduledRebuildCancellation, scheduledCts))
                    {
                        _scheduledRebuildCancellation.Dispose();
                        _scheduledRebuildCancellation = null;
                    }
                }
            }
        });
    }

    /// <summary>
    /// Cancels any pending debounced rebuild request.
    /// </summary>
    public void CancelPendingRebuild()
    {
        lock (_rebuildScheduleLock)
        {
            if (_scheduledRebuildCancellation is null)
            {
                return;
            }

            _scheduledRebuildCancellation.Cancel();
            _scheduledRebuildCancellation.Dispose();
            _scheduledRebuildCancellation = null;
        }
    }

#if JELLYFIN_10_11
    private Jellyfin.Database.Implementations.Entities.User? GetAnyQueryUser()
#else
    private Jellyfin.Data.Entities.User? GetAnyQueryUser()
#endif
    {
        try
        {
            var getFirstUserMethod = _userManager.GetType().GetMethod("GetFirstUser", Type.EmptyTypes);
            if (getFirstUserMethod is not null)
            {
                var firstUser = getFirstUserMethod.Invoke(_userManager, null);
                if (TryExtractUserId(firstUser, out var firstUserId))
                {
                    return _userManager.GetUserById(firstUserId);
                }
            }

            var usersProperty = _userManager.GetType().GetProperty("Users");
            if (usersProperty?.GetValue(_userManager) is IEnumerable usersEnumerable)
            {
                foreach (var rawUser in usersEnumerable)
                {
                    if (TryExtractUserId(rawUser, out var userId))
                    {
                        return _userManager.GetUserById(userId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[CastCrew] Failed to enumerate users for mapping query context");
        }

        return null;
    }

    private static bool TryExtractUserId(object? rawUser, out Guid userId)
    {
        userId = Guid.Empty;
        if (rawUser is null)
        {
            return false;
        }

        var idValue = rawUser.GetType().GetProperty("Id")?.GetValue(rawUser);
        if (idValue is Guid parsedUserId)
        {
            userId = parsedUserId;
            return true;
        }

        if (idValue is string userIdText && Guid.TryParse(userIdText, out parsedUserId))
        {
            userId = parsedUserId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a person (by name) appears in any of the specified libraries.
    /// Returns true if no library filter is active (empty includedLibraryIds).
    /// </summary>
    public bool IsPersonInLibraries(string personName, IReadOnlyList<string> includedLibraryIds)
    {
        // If no filter is configured, all persons pass
        if (includedLibraryIds is null || includedLibraryIds.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(personName))
        {
            return false;
        }

        var normalizedIncludedLibraryIds = includedLibraryIds
            .Select(NormalizeLibraryId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedIncludedLibraryIds.Length == 0)
        {
            return false;
        }

        var normalizedPersonName = personName.Trim();
        if (normalizedPersonName.Length == 0)
        {
            return false;
        }

        Dictionary<string, HashSet<string>> map;
        lock (_lock)
        {
            map = _personLibraryMap;
        }

        if (!map.TryGetValue(normalizedPersonName, out var personLibraries))
        {
            // Active filter means we only include people that exist in the mapping.
            return false;
        }

        foreach (var libraryId in normalizedIncludedLibraryIds)
        {
            if (personLibraries.Contains(libraryId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all library IDs a person appears in.
    /// </summary>
    public IReadOnlySet<string> GetLibraryIdsForPerson(string personName)
    {
        Dictionary<string, HashSet<string>> map;
        lock (_lock)
        {
            map = _personLibraryMap;
        }

        if (string.IsNullOrWhiteSpace(personName))
        {
            return new HashSet<string>();
        }

        var normalizedPersonName = personName.Trim();
        if (normalizedPersonName.Length == 0)
        {
            return new HashSet<string>();
        }

        if (map.TryGetValue(normalizedPersonName, out var libraryIds))
        {
            return libraryIds;
        }

        return new HashSet<string>();
    }

    private static string NormalizeLibraryId(string? libraryId)
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

    /// <summary>
    /// Gets all available virtual folders (libraries) from Jellyfin.
    /// Returns tuples of (Id, Name, CollectionType).
    /// </summary>
    public IReadOnlyList<(string Id, string Name, string CollectionType)> GetAvailableLibraries()
    {
        try
        {
            var virtualFolders = _libraryManager.GetVirtualFolders();
            var result = new List<(string, string, string)>();
            foreach (var f in virtualFolders)
            {
                if (!string.IsNullOrEmpty(f.ItemId))
                {
                    result.Add((f.ItemId, f.Name ?? "Unknown", f.CollectionType?.ToString() ?? ""));
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CastCrew] Failed to get available libraries");
            return Array.Empty<(string, string, string)>();
        }
    }
}
