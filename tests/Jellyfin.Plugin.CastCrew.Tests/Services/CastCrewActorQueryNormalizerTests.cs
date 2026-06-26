using Jellyfin.Plugin.CastCrew.Api;
using Jellyfin.Plugin.CastCrew.Configuration;
using Jellyfin.Plugin.CastCrew.Services;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests.Services;

public class CastCrewActorQueryNormalizerTests
{
    [Fact]
    public void Normalize_UsesConfigurationDefaults_WhenQueryValuesAreMissing()
    {
        var config = new PluginConfiguration
        {
            DefaultPageSize = 72,
            DefaultSortBy = CastCrewConfigurationDefaults.SortByDateCreated,
            DetailRoutePreference = CastCrewConfigurationDefaults.RoutePreferenceHash
        };

        var result = CastCrewActorQueryNormalizer.Normalize(new CastCrewActorsQuery(), config);

        Assert.Equal(0, result.StartIndex);
        Assert.Equal(72, result.Limit);
        Assert.Equal(CastCrewConfigurationDefaults.SortByDateCreated, result.SortBy);
        Assert.Equal(CastCrewConfigurationDefaults.RoutePreferenceHash, result.DetailRoutePreference);
        Assert.Null(result.SearchTerm);
        Assert.Null(result.Tag);
        Assert.Null(result.ProductionLocation);
    }

    [Fact]
    public void Normalize_ClampsInvalidPaging_AndTrimsSearchTerm()
    {
        var config = new PluginConfiguration
        {
            DefaultPageSize = 50,
            DefaultSortBy = CastCrewConfigurationDefaults.SortByName
        };

        var query = new CastCrewActorsQuery
        {
            StartIndex = -5,
            Limit = 9999,
            SearchTerm = "   morgan   ",
            Tag = "   award_winner   ",
            ProductionLocation = "  United States  "
        };

        var result = CastCrewActorQueryNormalizer.Normalize(query, config);

        Assert.Equal(0, result.StartIndex);
        Assert.Equal(CastCrewConfigurationDefaults.MaxPageSize, result.Limit);
        Assert.Equal("morgan", result.SearchTerm);
        Assert.Equal("award_winner", result.Tag);
        Assert.Equal("United States", result.ProductionLocation);
    }

    [Fact]
    public void NormalizeSortBy_FallsBackToName_WhenRequestedAndConfiguredValuesAreInvalid()
    {
        var normalizedSort = CastCrewActorQueryNormalizer.NormalizeSortBy("NewestFirst", "Alphabetical");

        Assert.Equal(CastCrewConfigurationDefaults.SortByName, normalizedSort);
    }

    [Fact]
    public void Normalize_FacetFiltersBecomeNull_WhenInputIsWhitespace()
    {
        var config = new PluginConfiguration();
        var query = new CastCrewActorsQuery
        {
            Tag = "   ",
            ProductionLocation = "\t"
        };

        var result = CastCrewActorQueryNormalizer.Normalize(query, config);

        Assert.Null(result.Tag);
        Assert.Null(result.ProductionLocation);
    }

    [Theory]
    [InlineData("hash", CastCrewConfigurationDefaults.RoutePreferenceHash)]
    [InlineData("HASHBANG", CastCrewConfigurationDefaults.RoutePreferenceHashBang)]
    [InlineData("unexpected", CastCrewConfigurationDefaults.RoutePreferenceAuto)]
    [InlineData(null, CastCrewConfigurationDefaults.RoutePreferenceAuto)]
    public void NormalizeDetailRoutePreference_NormalizesRouteMode(string? input, string expected)
    {
        var normalized = CastCrewActorQueryNormalizer.NormalizeDetailRoutePreference(input);

        Assert.Equal(expected, normalized);
    }
}
