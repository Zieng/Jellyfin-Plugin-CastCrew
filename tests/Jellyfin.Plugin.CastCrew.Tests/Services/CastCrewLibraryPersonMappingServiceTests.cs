using Jellyfin.Plugin.CastCrew.Services;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests.Services;

public class CastCrewLibraryPersonMappingServiceTests
{
    /// <summary>
    /// Creates a mapping service with test data injected via reflection.
    /// This bypasses ILibraryManager to test only the filtering logic.
    /// </summary>
    private static CastCrewLibraryPersonMappingService CreateServiceWithTestData(
        Dictionary<string, HashSet<string>> personLibraryMap,
        Dictionary<string, string>? mappedLibraries = null)
    {
#pragma warning disable SYSLIB0050
        var service = (CastCrewLibraryPersonMappingService)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(CastCrewLibraryPersonMappingService));
#pragma warning restore SYSLIB0050

        // Inject test map and lock
        var mapField = typeof(CastCrewLibraryPersonMappingService)
            .GetField("_personLibraryMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        mapField.SetValue(service, personLibraryMap);

        var lockField = typeof(CastCrewLibraryPersonMappingService)
            .GetField("_lock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        lockField.SetValue(service, new object());

        var mappedLibrariesField = typeof(CastCrewLibraryPersonMappingService)
            .GetField("_mappedLibraryNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        mappedLibrariesField.SetValue(
            service,
            mappedLibraries ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return service;
    }

    [Fact]
    public void IsPersonInLibraries_ReturnsTrue_WhenNoLibraryFilterConfigured()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" }
        };
        var service = CreateServiceWithTestData(map);

        // Empty filter means all pass
        Assert.True(service.IsPersonInLibraries("Tom Hanks", Array.Empty<string>()));
        Assert.True(service.IsPersonInLibraries("Tom Hanks", null!));
    }

    [Fact]
    public void IsPersonInLibraries_ReturnsTrue_WhenPersonIsInIncludedLibrary()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1", "lib-2" },
            ["Morgan Freeman"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-2" }
        };
        var service = CreateServiceWithTestData(map);

        Assert.True(service.IsPersonInLibraries("Tom Hanks", new[] { "lib-1" }));
        Assert.True(service.IsPersonInLibraries("Tom Hanks", new[] { "lib-2" }));
        Assert.True(service.IsPersonInLibraries("Morgan Freeman", new[] { "lib-2" }));
    }

    [Fact]
    public void IsPersonInLibraries_ReturnsFalse_WhenPersonNotInIncludedLibrary()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" },
            ["Morgan Freeman"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-2" }
        };
        var service = CreateServiceWithTestData(map);

        Assert.False(service.IsPersonInLibraries("Tom Hanks", new[] { "lib-2" }));
        Assert.False(service.IsPersonInLibraries("Morgan Freeman", new[] { "lib-1" }));
    }

    [Fact]
    public void IsPersonInLibraries_IsCaseInsensitive_ForPersonName()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" }
        };
        var service = CreateServiceWithTestData(map);

        Assert.True(service.IsPersonInLibraries("tom hanks", new[] { "lib-1" }));
        Assert.True(service.IsPersonInLibraries("TOM HANKS", new[] { "lib-1" }));
    }

    [Fact]
    public void IsPersonInLibraries_ReturnsFalse_WhenPersonNotInMappingWithActiveFilter()
    {
        // Active library filter only includes people that are explicitly mapped.
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" }
        };
        var service = CreateServiceWithTestData(map);

        Assert.False(service.IsPersonInLibraries("Unknown Person", new[] { "lib-1" }));
    }

    [Fact]
    public void IsPersonInLibraries_NormalizesGuidLibraryIds()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "d9ebfb8a0f684fc6a86dd0bb6f773499" }
        };
        var service = CreateServiceWithTestData(map);

        Assert.True(service.IsPersonInLibraries("Tom Hanks", new[] { "d9ebfb8a-0f68-4fc6-a86d-d0bb6f773499" }));
    }

    [Fact]
    public void IsPersonInLibraries_ReturnsFalse_WhenPersonNameIsNullOrWhitespace()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var service = CreateServiceWithTestData(map);

        Assert.False(service.IsPersonInLibraries("", new[] { "lib-1" }));
        Assert.False(service.IsPersonInLibraries("   ", new[] { "lib-1" }));
        Assert.False(service.IsPersonInLibraries(null!, new[] { "lib-1" }));
    }

    [Fact]
    public void IsPersonInLibraries_MatchesAnyIncludedLibrary()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" },
            ["Morgan Freeman"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-3" }
        };
        var service = CreateServiceWithTestData(map);

        // Tom is in lib-1, filter includes lib-1 and lib-2
        Assert.True(service.IsPersonInLibraries("Tom Hanks", new[] { "lib-1", "lib-2" }));
        // Morgan is in lib-3, filter includes lib-1 and lib-2
        Assert.False(service.IsPersonInLibraries("Morgan Freeman", new[] { "lib-1", "lib-2" }));
    }

    [Fact]
    public void GetLibraryIdsForPerson_ReturnsEmpty_WhenPersonNotInMapping()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1" }
        };
        var service = CreateServiceWithTestData(map);

        var result = service.GetLibraryIdsForPerson("Unknown Person");
        Assert.Empty(result);
    }

    [Fact]
    public void GetLibraryIdsForPerson_ReturnsCorrectLibraries()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tom Hanks"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lib-1", "lib-2" }
        };
        var service = CreateServiceWithTestData(map);

        var result = service.GetLibraryIdsForPerson("Tom Hanks");
        Assert.Contains("lib-1", result);
        Assert.Contains("lib-2", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetMappedLibraries_ReturnsSortedLibraries()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var mappedLibraries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lib-b"] = "Bravo",
            ["lib-a"] = "Alpha"
        };
        var service = CreateServiceWithTestData(map, mappedLibraries);

        var result = service.GetMappedLibraries();

        Assert.Equal(2, result.Count);
        Assert.Equal("lib-a", result[0].Id);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("lib-b", result[1].Id);
        Assert.Equal("Bravo", result[1].Name);
    }
}
