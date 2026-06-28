using Jellyfin.Plugin.CastCrew.Api;
using Jellyfin.Plugin.CastCrew.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Services;

public sealed class CastCrewActorQueryService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;
    private readonly CastCrewLibraryPersonMappingService _mappingService;
    private readonly ILogger<CastCrewActorQueryService> _logger;

    public CastCrewActorQueryService(
        ILibraryManager libraryManager,
        IDtoService dtoService,
        CastCrewLibraryPersonMappingService mappingService,
        ILogger<CastCrewActorQueryService> logger)
    {
        _libraryManager = libraryManager;
        _dtoService = dtoService;
        _mappingService = mappingService;
        _logger = logger;
    }

#if JELLYFIN_10_11
    public CastCrewActorsResponse QueryActors(CastCrewActorsQuery query, Jellyfin.Database.Implementations.Entities.User user)
#else
    public CastCrewActorsResponse QueryActors(CastCrewActorsQuery query, Jellyfin.Data.Entities.User user)
#endif
        => QueryPersons(query, user, "Actor");

#if JELLYFIN_10_11
    public CastCrewActorsResponse QueryDirectors(CastCrewActorsQuery query, Jellyfin.Database.Implementations.Entities.User user)
#else
    public CastCrewActorsResponse QueryDirectors(CastCrewActorsQuery query, Jellyfin.Data.Entities.User user)
#endif
        => QueryPersons(query, user, "Director");

#if JELLYFIN_10_11
    public CastCrewActorsResponse QueryProducers(CastCrewActorsQuery query, Jellyfin.Database.Implementations.Entities.User user)
#else
    public CastCrewActorsResponse QueryProducers(CastCrewActorsQuery query, Jellyfin.Data.Entities.User user)
#endif
        => QueryPersons(query, user, "Producer");

#if JELLYFIN_10_11
    private CastCrewActorsResponse QueryPersons(CastCrewActorsQuery query, Jellyfin.Database.Implementations.Entities.User user, string personType)
#else
    private CastCrewActorsResponse QueryPersons(CastCrewActorsQuery query, Jellyfin.Data.Entities.User user, string personType)
#endif
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        var normalizedQuery = CastCrewActorQueryNormalizer.Normalize(query, configuration);
        _mappingService.EnsureMappingBuilt();

        var availableLibraries = _mappingService
            .GetMappedLibraries()
            .Select(library => new CastCrewLibraryOption
            {
                Id = library.Id,
                Name = library.Name
            })
            .ToArray();
        var libraryMappingLastSyncedUtc = NormalizeMappingSyncTime(_mappingService.LastBuildTime);

        var effectiveLibraryIds = ResolveEffectiveLibraryIds(
            configuration.IncludedLibraryIds,
            normalizedQuery.RequestedLibraryIds);

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Querying {PersonType}. SearchTerm={SearchTerm}, StartIndex={StartIndex}, Limit={Limit}, EffectiveLibraryFilter={LibraryFilterCount}.",
            personType,
            normalizedQuery.SearchTerm ?? "(none)",
            normalizedQuery.StartIndex,
            normalizedQuery.Limit,
            effectiveLibraryIds.Length);

        var dtoOptions = new DtoOptions
        {
            EnableImages = true,
            ImageTypes = new[] { ImageType.Primary },
            ImageTypeLimit = 1,
            Fields = new[] { ItemFields.Overview, ItemFields.DateCreated, ItemFields.Tags, ItemFields.ProductionLocations }
        };

        // When a search term is present, return grouped results (name matches + description matches)
        if (!string.IsNullOrEmpty(normalizedQuery.SearchTerm))
        {
            return QueryPersonsGrouped(
                normalizedQuery,
                user,
                personType,
                dtoOptions,
                effectiveLibraryIds,
                availableLibraries,
                libraryMappingLastSyncedUtc);
        }

        // No search term: return flat list with pagination (existing behavior)
        IReadOnlyList<Person> allPersons;
        try
        {
            var peopleQuery = new InternalPeopleQuery(new[] { personType }, new[] { string.Empty })
            {
                User = user,
                NameContains = null,
                IsFavorite = normalizedQuery.IsFavorite,
                AppearsInItemId = Guid.Empty,
                Limit = 0
            };

            allPersons = _libraryManager.GetPeopleItems(peopleQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CastCrew] Failed to query {PersonType} items.", personType);
            return new CastCrewActorsResponse
            {
                Items = Array.Empty<MediaBrowser.Model.Dto.BaseItemDto>(),
                TotalRecordCount = 0,
                StartIndex = normalizedQuery.StartIndex,
                PageSize = normalizedQuery.Limit,
                SortBy = normalizedQuery.SortBy,
                DetailRoutePreference = normalizedQuery.DetailRoutePreference,
                AvailableTags = Array.Empty<string>(),
                AvailableProductionLocations = Array.Empty<string>(),
                AvailableLibraries = availableLibraries,
                LibraryMappingLastSyncedUtc = libraryMappingLastSyncedUtc
            };
        }

        // Apply library filter
        var libraryFilteredPersons = ApplyLibraryFilter(allPersons, effectiveLibraryIds);

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Filtered {PersonType} results by libraries: {BeforeCount} -> {AfterCount}.",
            personType,
            allPersons.Count,
            libraryFilteredPersons.Count);

        var availableTags = GetAvailableFacetValues(libraryFilteredPersons, person => person.Tags);
        var availableProductionLocations = GetAvailableFacetValues(
            libraryFilteredPersons,
            person => person.ProductionLocations,
            NormalizeProductionLocationFacetValue);
        var filteredPersons = ApplyFacetFilters(libraryFilteredPersons, normalizedQuery).ToArray();
        var orderedPersons = ApplySorting(filteredPersons, normalizedQuery);

        var pagedPersons = orderedPersons
            .Skip(normalizedQuery.StartIndex)
            .Take(normalizedQuery.Limit)
            .ToArray();

        var items = pagedPersons
            .Select(person => _dtoService.GetItemByNameDto(person, dtoOptions, null, user))
            .ToArray();

        return new CastCrewActorsResponse
        {
            Items = items,
            TotalRecordCount = filteredPersons.Length,
            StartIndex = normalizedQuery.StartIndex,
            PageSize = normalizedQuery.Limit,
            SortBy = normalizedQuery.SortBy,
            DetailRoutePreference = normalizedQuery.DetailRoutePreference,
            AvailableTags = availableTags,
            AvailableProductionLocations = availableProductionLocations,
            AvailableLibraries = availableLibraries,
            LibraryMappingLastSyncedUtc = libraryMappingLastSyncedUtc
        };
    }

    private CastCrewActorsResponse QueryPersonsGrouped(
        NormalizedCastCrewActorQuery normalizedQuery,
#if JELLYFIN_10_11
        Jellyfin.Database.Implementations.Entities.User user,
#else
        Jellyfin.Data.Entities.User user,
#endif
        string personType,
        DtoOptions dtoOptions,
        IReadOnlyList<string> effectiveLibraryIds,
        IReadOnlyList<CastCrewLibraryOption> availableLibraries,
        DateTime? libraryMappingLastSyncedUtc)
    {
        IReadOnlyList<Person> nameMatchPersons;
        IReadOnlyList<Person> allPersons;

        try
        {
            // Query name matches
            var nameQuery = new InternalPeopleQuery(new[] { personType }, new[] { string.Empty })
            {
                User = user,
                NameContains = normalizedQuery.SearchTerm,
                IsFavorite = normalizedQuery.IsFavorite,
                AppearsInItemId = Guid.Empty,
                Limit = 0
            };
            nameMatchPersons = _libraryManager.GetPeopleItems(nameQuery);

            // Query all persons of this type (for description matching)
            var allQuery = new InternalPeopleQuery(new[] { personType }, new[] { string.Empty })
            {
                User = user,
                NameContains = null,
                IsFavorite = normalizedQuery.IsFavorite,
                AppearsInItemId = Guid.Empty,
                Limit = 0
            };
            allPersons = _libraryManager.GetPeopleItems(allQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CastCrew] Failed to query grouped {PersonType} items.", personType);
            return new CastCrewActorsResponse
            {
                Items = Array.Empty<MediaBrowser.Model.Dto.BaseItemDto>(),
                TotalRecordCount = 0,
                StartIndex = normalizedQuery.StartIndex,
                PageSize = normalizedQuery.Limit,
                SortBy = normalizedQuery.SortBy,
                DetailRoutePreference = normalizedQuery.DetailRoutePreference,
                NameMatchItems = Array.Empty<MediaBrowser.Model.Dto.BaseItemDto>(),
                NameMatchCount = 0,
                DescriptionMatchItems = Array.Empty<MediaBrowser.Model.Dto.BaseItemDto>(),
                DescriptionMatchCount = 0,
                AvailableTags = Array.Empty<string>(),
                AvailableProductionLocations = Array.Empty<string>(),
                AvailableLibraries = availableLibraries,
                LibraryMappingLastSyncedUtc = libraryMappingLastSyncedUtc
            };
        }

        // Apply library filter
        nameMatchPersons = ApplyLibraryFilter(nameMatchPersons, effectiveLibraryIds);
        allPersons = ApplyLibraryFilter(allPersons, effectiveLibraryIds);

        CastCrewDebugLogging.LogInformation(
            _logger,
            "Grouped query for {PersonType}: NameMatchesAfterLibraryFilter={NameMatchCount}, AllAfterLibraryFilter={AllCount}, EffectiveLibraryFilter={LibraryFilterCount}.",
            personType,
            nameMatchPersons.Count,
            allPersons.Count,
            effectiveLibraryIds.Count);

        var availableTags = GetAvailableFacetValues(allPersons, person => person.Tags);
        var availableProductionLocations = GetAvailableFacetValues(
            allPersons,
            person => person.ProductionLocations,
            NormalizeProductionLocationFacetValue);
        var filteredNameMatchPersons = ApplyFacetFilters(nameMatchPersons, normalizedQuery).ToArray();
        var filteredAllPersons = ApplyFacetFilters(allPersons, normalizedQuery).ToArray();

        // Build set of name-match IDs for exclusion
        var nameMatchIds = new HashSet<Guid>(filteredNameMatchPersons.Select(p => p.Id));

        // Filter description matches: overview contains search term, but not already a name match
        var searchTerm = normalizedQuery.SearchTerm!;
        var descriptionMatchPersons = filteredAllPersons
            .Where(p => !nameMatchIds.Contains(p.Id)
                && !string.IsNullOrEmpty(p.Overview)
                && p.Overview.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Apply sorting to both groups
        var sortedNameMatches = ApplySorting(filteredNameMatchPersons, normalizedQuery).ToArray();
        var sortedDescMatches = ApplySorting(descriptionMatchPersons, normalizedQuery).ToArray();

        // Limit each group to page size
        var pagedNameMatches = sortedNameMatches.Take(normalizedQuery.Limit).ToArray();
        var pagedDescMatches = sortedDescMatches.Take(normalizedQuery.Limit).ToArray();

        var nameMatchDtos = pagedNameMatches
            .Select(person => _dtoService.GetItemByNameDto(person, dtoOptions, null, user))
            .ToArray();

        var descMatchDtos = pagedDescMatches
            .Select(person => _dtoService.GetItemByNameDto(person, dtoOptions, null, user))
            .ToArray();

        // Combined items for backward compatibility
        var combinedItems = nameMatchDtos.Concat(descMatchDtos).ToArray();

        return new CastCrewActorsResponse
        {
            Items = combinedItems,
            TotalRecordCount = sortedNameMatches.Length + sortedDescMatches.Length,
            StartIndex = 0,
            PageSize = normalizedQuery.Limit,
            SortBy = normalizedQuery.SortBy,
            DetailRoutePreference = normalizedQuery.DetailRoutePreference,
            NameMatchItems = nameMatchDtos,
            NameMatchCount = sortedNameMatches.Length,
            DescriptionMatchItems = descMatchDtos,
            DescriptionMatchCount = sortedDescMatches.Length,
            AvailableTags = availableTags,
            AvailableProductionLocations = availableProductionLocations,
            AvailableLibraries = availableLibraries,
            LibraryMappingLastSyncedUtc = libraryMappingLastSyncedUtc
        };
    }

    private IReadOnlyList<Person> ApplyLibraryFilter(IReadOnlyList<Person> persons, IReadOnlyList<string> includedLibraryIds)
    {
        if (includedLibraryIds is null || includedLibraryIds.Count == 0)
        {
            return persons;
        }

        _mappingService.EnsureMappingBuilt();

        return persons
            .Where(person => _mappingService.IsPersonInLibraries(person.Name, includedLibraryIds))
            .ToList();
    }

    private static string[] ResolveEffectiveLibraryIds(
        IReadOnlyList<string>? configuredLibraryIds,
        IReadOnlyList<string>? requestedLibraryIds)
    {
        var normalizedConfigured = (configuredLibraryIds ?? Array.Empty<string>())
            .Select(CastCrewLibraryIdNormalizer.NormalizeLibraryId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedRequested = (requestedLibraryIds ?? Array.Empty<string>())
            .Select(CastCrewLibraryIdNormalizer.NormalizeLibraryId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRequested.Length == 0)
        {
            return normalizedConfigured;
        }

        if (normalizedConfigured.Length == 0)
        {
            return normalizedRequested;
        }

        var configuredSet = normalizedConfigured.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return normalizedRequested
            .Where(configuredSet.Contains)
            .ToArray();
    }

    private static DateTime? NormalizeMappingSyncTime(DateTime timestamp)
    {
        if (timestamp == DateTime.MinValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
    }

    private static IEnumerable<Person> ApplyFacetFilters(
        IEnumerable<Person> persons,
        NormalizedCastCrewActorQuery normalizedQuery)
    {
        var filtered = persons;

        if (!string.IsNullOrEmpty(normalizedQuery.Tag))
        {
            filtered = filtered.Where(person => ContainsFacetValue(person.Tags, normalizedQuery.Tag));
        }

        if (!string.IsNullOrEmpty(normalizedQuery.ProductionLocation))
        {
            filtered = filtered.Where(person => ContainsProductionLocationFacetValue(person.ProductionLocations, normalizedQuery.ProductionLocation));
        }

        return filtered;
    }

    private static IReadOnlyList<string> GetAvailableFacetValues(
        IEnumerable<Person> persons,
        Func<Person, IReadOnlyList<string>?> selector,
        Func<string, string>? normalizeValue = null)
    {
        var normalize = normalizeValue ?? (value => value.Trim());

        return persons
            .SelectMany(person => selector(person) ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ContainsFacetValue(IReadOnlyList<string>? values, string expected)
    {
        if (values is null || values.Count == 0)
        {
            return false;
        }

        return values.Any(value => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsProductionLocationFacetValue(IReadOnlyList<string>? values, string expected)
    {
        if (values is null || values.Count == 0)
        {
            return false;
        }

        var normalizedExpected = NormalizeProductionLocationFacetValue(expected);
        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return false;
        }

        return values.Any(value => string.Equals(
            NormalizeProductionLocationFacetValue(value),
            normalizedExpected,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProductionLocationFacetValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var segments = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        return segments[^1];
    }

    private static IEnumerable<Person> ApplySorting(IEnumerable<Person> persons, NormalizedCastCrewActorQuery normalizedQuery)
    {
        if (normalizedQuery.SortBy == CastCrewConfigurationDefaults.SortByRandom)
        {
            return persons.OrderBy(_ => Random.Shared.Next());
        }

        if (normalizedQuery.SortBy == CastCrewConfigurationDefaults.SortByDateCreated)
        {
            return normalizedQuery.SortOrder == CastCrewConfigurationDefaults.SortOrderDescending
                ? persons.OrderByDescending(person => person.DateCreated)
                : persons.OrderBy(person => person.DateCreated);
        }

        return normalizedQuery.SortOrder == CastCrewConfigurationDefaults.SortOrderDescending
            ? persons.OrderByDescending(
                person => person.SortName ?? person.Name ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            : persons.OrderBy(
                person => person.SortName ?? person.Name ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }
}
