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
    private const int MaxLoggedPeoplePerMovie = 120;

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<CastCrewLibraryPersonMappingService> _logger;
    private readonly object _lock = new();
    private readonly object _rebuildLock = new();
    private readonly object _rebuildScheduleLock = new();

    // personName (case-insensitive key) → set of library ItemIds (as strings)
    private Dictionary<string, HashSet<string>> _personLibraryMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _mappedLibraryNames = new(StringComparer.OrdinalIgnoreCase);
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
            var debugLoggingEnabled = CastCrewDebugLogging.IsEnabled();

            _logger.LogInformation("[CastCrew] Building person-to-library mapping...");
            if (debugLoggingEnabled)
            {
                _logger.LogInformation("[CastCrew][Debug] Starting person-to-library mapping scan.");
            }

            var newMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var debugLibraryStats = new Dictionary<string, (int MoviesScanned, HashSet<string> PersonNames)>(StringComparer.OrdinalIgnoreCase);
            var mappedLibraryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var configuration = CastCrewPlugin.Instance?.Configuration;
                var configuredIncludedLibraryIds = (configuration?.IncludedLibraryIds ?? Array.Empty<string>())
                    .Select(CastCrewLibraryIdNormalizer.NormalizeLibraryId)
                    .Where(id => id.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var virtualFolders = _libraryManager.GetVirtualFolders();
                var libraryNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var libraryIdSet = new HashSet<string>(
                    virtualFolders
                        .Select(f =>
                        {
                            var id = CastCrewLibraryIdNormalizer.NormalizeLibraryId(f.ItemId);
                            if (id.Length > 0)
                            {
                                libraryNameById[id] = string.IsNullOrWhiteSpace(f.Name) ? id : f.Name;
                            }

                            return id;
                        })
                        .Where(id => id.Length > 0 && (configuredIncludedLibraryIds.Count == 0 || configuredIncludedLibraryIds.Contains(id))),
                    StringComparer.OrdinalIgnoreCase);

                if (libraryIdSet.Count == 0)
                {
                    _logger.LogInformation("[CastCrew] No libraries found, mapping is empty");
                    if (debugLoggingEnabled && configuredIncludedLibraryIds.Count > 0)
                    {
                        _logger.LogInformation(
                            "[CastCrew][Debug] Included libraries are configured but none matched virtual folders: {IncludedLibraryIds}",
                            string.Join(", ", configuredIncludedLibraryIds));
                    }

                    lock (_lock)
                    {
                        _personLibraryMap = newMap;
                        _mappedLibraryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

                if (debugLoggingEnabled)
                {
                    var includedLibraryNames = libraryIdSet
                        .Select(libraryId => ResolveLibraryName(libraryId, libraryNameById))
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    _logger.LogInformation(
                        "[CastCrew][Debug] Found included libraries: {IncludedLibraries}",
                        includedLibraryNames.Length == 0 ? "(none)" : string.Join(", ", includedLibraryNames));
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
                    var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id.ToString("N") : item.Name;
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
                        var folderId = CastCrewLibraryIdNormalizer.NormalizeLibraryId(folder.Id.ToString("N"));
                        if (folderId.Length > 0 && libraryIdSet.Contains(folderId))
                        {
                            itemLibraryIds.Add(folderId);
                        }
                    }

                    if (itemLibraryIds.Count == 0)
                    {
                        continue;
                    }

                    var mappedPeopleForMovie = new List<string>();
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

                        if (debugLoggingEnabled)
                        {
                            mappedPeopleForMovie.Add(normalizedPersonName);
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

                    if (debugLoggingEnabled)
                    {
                        var distinctMoviePeople = mappedPeopleForMovie
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        foreach (var libraryId in itemLibraryIds)
                        {
                            if (!debugLibraryStats.TryGetValue(libraryId, out var stats))
                            {
                                stats = (0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                            }

                            stats.MoviesScanned++;
                            foreach (var personName in distinctMoviePeople)
                            {
                                stats.PersonNames.Add(personName);
                            }

                            debugLibraryStats[libraryId] = stats;
                        }

                        var loggedPeople = distinctMoviePeople
                            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                            .Take(MaxLoggedPeoplePerMovie)
                            .ToArray();

                        var libraryLabel = string.Join(", ", itemLibraryIds
                            .Select(id => ResolveLibraryName(id, libraryNameById))
                            .Distinct(StringComparer.OrdinalIgnoreCase));

                        _logger.LogInformation(
                            "[CastCrew][Debug] Found movie \"{MovieName}\" in {LibraryName} with cast/crew: {People}.",
                            itemName,
                            libraryLabel,
                            loggedPeople.Length == 0 ? "(none)" : string.Join(", ", loggedPeople));
                    }
                }

                if (debugLoggingEnabled)
                {
                    foreach (var libraryId in libraryIdSet.OrderBy(id => ResolveLibraryName(id, libraryNameById), StringComparer.OrdinalIgnoreCase))
                    {
                        var hasStats = debugLibraryStats.TryGetValue(libraryId, out var stats);
                        var movieCount = hasStats ? stats.MoviesScanned : 0;
                        var personCount = hasStats ? stats.PersonNames.Count : 0;
                        var libraryName = ResolveLibraryName(libraryId, libraryNameById);

                        _logger.LogInformation(
                            "[CastCrew][Debug] Person-to-library mapping finished for {LibraryName}: scanned {MovieCount} movies, found {PersonCount} people.",
                            libraryName,
                            movieCount,
                            personCount);
                    }
                }

                mappedLibraryNames = libraryIdSet
                    .ToDictionary(
                        libraryId => libraryId,
                        libraryId => ResolveLibraryName(libraryId, libraryNameById),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CastCrew] Failed to build person-to-library mapping");
                return;
            }

            lock (_lock)
            {
                _personLibraryMap = newMap;
                _mappedLibraryNames = mappedLibraryNames;
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
            CastCrewDebugLogging.LogInformation(_logger, "Mapping already initialized at {LastBuildTimeUtc}.", LastBuildTime);
            return;
        }

        CastCrewDebugLogging.LogInformation(_logger, "Mapping not initialized yet; rebuilding immediately.");
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
        var debugLoggingEnabled = CastCrewDebugLogging.IsEnabled();

        CancellationTokenSource scheduledCts;
        lock (_rebuildScheduleLock)
        {
            _scheduledRebuildCancellation?.Cancel();
            _scheduledRebuildCancellation?.Dispose();

            _scheduledRebuildCancellation = new CancellationTokenSource();
            scheduledCts = _scheduledRebuildCancellation;
        }

        if (debugLoggingEnabled)
        {
            _logger.LogInformation(
                "[CastCrew][Debug] Scheduled person-to-library mapping rebuild in {DelayMs} ms (reason: {Reason})",
                Math.Max((int)delay.TotalMilliseconds, 0),
                reason);
        }
        else
        {
            _logger.LogDebug(
                "[CastCrew] Scheduled person-to-library mapping rebuild in {DelayMs} ms (reason: {Reason})",
                Math.Max((int)delay.TotalMilliseconds, 0),
                reason);
        }

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
            .Select(CastCrewLibraryIdNormalizer.NormalizeLibraryId)
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
    /// Gets the set of libraries included in the latest mapping build.
    /// </summary>
    public IReadOnlyList<(string Id, string Name)> GetMappedLibraries()
    {
        Dictionary<string, string> mappedLibraryNames;
        lock (_lock)
        {
            mappedLibraryNames = _mappedLibraryNames;
        }

        if (mappedLibraryNames.Count == 0)
        {
            return Array.Empty<(string Id, string Name)>();
        }

        return mappedLibraryNames
            .Select(entry => (entry.Key, entry.Value))
            .OrderBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static string ResolveLibraryName(string libraryId, IReadOnlyDictionary<string, string> libraryNameById)
    {
        if (libraryNameById.TryGetValue(libraryId, out var libraryName) && !string.IsNullOrWhiteSpace(libraryName))
        {
            return libraryName;
        }

        return libraryId;
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

            CastCrewDebugLogging.LogInformation(_logger, "Resolved {LibraryCount} virtual folders for configuration.", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CastCrew] Failed to get available libraries");
            return Array.Empty<(string, string, string)>();
        }
    }

}
