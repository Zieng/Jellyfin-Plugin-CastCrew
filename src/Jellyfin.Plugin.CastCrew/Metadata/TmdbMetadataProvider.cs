using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.Plugin.CastCrew.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew.Metadata;

public sealed class TmdbMetadataProvider : IMetadataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbMetadataProvider> _logger;

    public TmdbMetadataProvider(ILogger<TmdbMetadataProvider> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-CastCrew/2.0");
    }

    public string Name => "TMDB";

    public async Task<IReadOnlyList<MetadataPerson>> SearchAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        var configuration = CastCrewPlugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!configuration.EnableTmdbProvider || string.IsNullOrWhiteSpace(configuration.TmdbApiToken))
        {
            return Array.Empty<MetadataPerson>();
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.TmdbApiToken.Trim());
        var type = query.Type == MetadataPersonType.Studio ? "company" : "person";
        if (query.Type == MetadataPersonType.Studio)
        {
            return await SearchCompaniesAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var searchName = Uri.EscapeDataString(string.IsNullOrWhiteSpace(query.Name) ? "a" : query.Name.Trim());
        var page = Math.Max(1, query.StartIndex / Math.Max(1, query.Limit) + 1);
        var response = await _httpClient.GetAsync($"search/person?query={searchName}&include_adult=true&page={page}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var result = new List<MetadataPerson>();
        foreach (var item in document.RootElement.GetProperty("results").EnumerateArray())
        {
            if (query.Type == MetadataPersonType.Director &&
                item.TryGetProperty("known_for_department", out var department) &&
                !string.Equals(department.GetString(), "Directing", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = item.GetProperty("id").GetInt32().ToString();
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.Add(new MetadataPerson
            {
                Id = $"tmdb:{id}", Source = Name, Name = name!, ExternalId = id,
                ProfileUrl = $"https://www.themoviedb.org/person/{id}",
                ImageUrl = item.TryGetProperty("profile_path", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } path
                    ? "https://image.tmdb.org/t/p/w342" + path : null,
                Gender = item.TryGetProperty("gender", out var g) ? g.GetInt32().ToString() : null,
                Overview = null
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<MetadataPerson>> SearchCompaniesAsync(MetadataSearchQuery query, CancellationToken cancellationToken)
    {
        var searchName = Uri.EscapeDataString(string.IsNullOrWhiteSpace(query.Name) ? "a" : query.Name.Trim());
        var page = Math.Max(1, query.StartIndex / Math.Max(1, query.Limit) + 1);
        var response = await _httpClient.GetAsync($"search/company?query={searchName}&page={page}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.GetProperty("results").EnumerateArray().Select(item =>
        {
            var id = item.GetProperty("id").GetInt32().ToString();
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            return new MetadataPerson
            {
                Id = $"tmdb-company:{id}", Source = Name, Name = name, ExternalId = id,
                ProfileUrl = $"https://www.themoviedb.org/company/{id}",
                ImageUrl = item.TryGetProperty("logo_path", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } path
                    ? "https://image.tmdb.org/t/p/w342" + path : null,
                Country = item.TryGetProperty("origin_country", out var c) ? c.GetString() : null
            };
        }).ToArray();
    }
}
