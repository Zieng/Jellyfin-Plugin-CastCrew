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
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<CastCrewLibraryPersonMappingService> _logger;
    private readonly object _lock = new();

    // personName (case-insensitive key) → set of library ItemIds (as strings)
    private Dictionary<string, HashSet<string>> _personLibraryMap = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastBuildTime = DateTime.MinValue;

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
        _logger.LogInformation("[CastCrew] Building person-to-library mapping...");

        var newMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var virtualFolders = _libraryManager.GetVirtualFolders();
            var libraryIdSet = new HashSet<string>(
                virtualFolders
                    .Where(f => !string.IsNullOrEmpty(f.ItemId))
                    .Select(f => f.ItemId),
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

            // Get any user for the item query context (required for Jellyfin item access)
            var queryUser = _userManager.Users?.FirstOrDefault();

            if (queryUser is null)
            {
                _logger.LogWarning("[CastCrew] No users found, cannot build person-library mapping");
                lock (_lock)
                {
                    _personLibraryMap = newMap;
                    _lastBuildTime = DateTime.UtcNow;
                }
                return;
            }

            // Query all media items (movies, series, episodes, etc.)
            var allItems = _libraryManager.GetItemList(new InternalItemsQuery
            {
                User = queryUser,
                Recursive = true,
                IsVirtualItem = false
            });

            _logger.LogInformation("[CastCrew] Found {Count} total items to process for library mapping", allItems.Count);

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
                    var folderId = folder.Id.ToString("N");
                    if (libraryIdSet.Contains(folderId))
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

                    if (!newMap.TryGetValue(personInfo.Name, out var personLibraries))
                    {
                        personLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        newMap[personInfo.Name] = personLibraries;
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

        Dictionary<string, HashSet<string>> map;
        lock (_lock)
        {
            map = _personLibraryMap;
        }

        if (!map.TryGetValue(personName, out var personLibraries))
        {
            // Person not in mapping — could be newly added; include by default
            return true;
        }

        foreach (var libraryId in includedLibraryIds)
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

        if (map.TryGetValue(personName, out var libraryIds))
        {
            return libraryIds;
        }

        return new HashSet<string>();
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
