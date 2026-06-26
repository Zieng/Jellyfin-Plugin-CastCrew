using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace Jellyfin.Plugin.CastCrew.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class JellyfinIntegrationFactAttribute : FactAttribute
{
    public JellyfinIntegrationFactAttribute()
    {
        var baseUrl = Environment.GetEnvironmentVariable("CASTCREW_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://127.0.0.1:8096/";
        }
        else
        {
            baseUrl = baseUrl.TrimEnd('/') + "/";
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASTCREW_API_KEY"));
        var hasUsername = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASTCREW_USERNAME"));
        var hasPassword = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASTCREW_PASSWORD"));

        if (!string.Equals(Environment.GetEnvironmentVariable("CASTCREW_RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip =
                "Set CASTCREW_RUN_INTEGRATION_TESTS=true to run Jellyfin host integration tests. " +
                "These tests are opt-in to avoid failing local runs without a configured host.";
            return;
        }

        if (!hasApiKey && !(hasUsername && hasPassword))
        {
            Skip =
                "Provide CASTCREW_API_KEY or CASTCREW_USERNAME/CASTCREW_PASSWORD " +
                "to run Jellyfin host integration tests.";
            return;
        }

        if (!IsJellyfinReachable(baseUrl))
        {
            Skip =
                $"No Jellyfin host is reachable at '{baseUrl}'. " +
                "Set CASTCREW_BASE_URL to a running Jellyfin instance.";
        }
    }

    private static bool IsJellyfinReachable(string baseUrl)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(3)
            };

            using var response = client.GetAsync("System/Info/Public").GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class CastCrewActorsEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [JellyfinIntegrationFact]
    public async Task GetActors_ReturnsExpectedContract_WhenHostAndAuthConfigured()
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = await CreateConfiguredClientAsync(settings);

        var actorsUrl = settings.BuildActorsEndpointUrl(limit: 5, sortBy: "Name");
        using var response = await client.GetAsync(actorsUrl);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new XunitException(
                "CastCrew endpoint was not found on the target Jellyfin host. " +
                "Ensure the plugin is deployed/enabled and /CastCrew/Actors is reachable.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new XunitException(
                "Authentication was rejected by Jellyfin. " +
                "Provide a valid CASTCREW_API_KEY or CASTCREW_USERNAME/CASTCREW_PASSWORD.");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CastCrewActorsResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(payload.Items);
        Assert.True(payload.TotalRecordCount >= 0);
        Assert.True(payload.StartIndex >= 0);
        Assert.True(payload.PageSize >= 10);
        Assert.True(payload.SortBy is "Name" or "DateCreated");
        Assert.True(payload.DetailRoutePreference is "Auto" or "HashBang" or "Hash");
    }

    [JellyfinIntegrationFact]
    public async Task GetActors_AcceptsDateCreatedSort_WhenHostAndAuthConfigured()
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = await CreateConfiguredClientAsync(settings);

        var actorsUrl = settings.BuildActorsEndpointUrl(limit: 10, sortBy: "DateCreated");
        using var response = await client.GetAsync(actorsUrl);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new XunitException(
                "CastCrew endpoint was not found on the target Jellyfin host. " +
                "Ensure the plugin is deployed/enabled and /CastCrew/Actors is reachable.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new XunitException(
                "Authentication was rejected by Jellyfin. " +
                "Provide a valid CASTCREW_API_KEY or CASTCREW_USERNAME/CASTCREW_PASSWORD.");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CastCrewActorsResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal("DateCreated", payload.SortBy);
    }

    [JellyfinIntegrationFact]
    public async Task GetDirectors_ReturnsExpectedContract_WhenHostAndAuthConfigured()
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = await CreateConfiguredClientAsync(settings);

        var url = settings.BuildDirectorsEndpointUrl(limit: 5, sortBy: "Name");
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CastCrewActorsResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(payload.Items);
        Assert.True(payload.TotalRecordCount >= 0);
        Assert.Equal("Name", payload.SortBy);
    }

    [JellyfinIntegrationFact]
    public async Task GetProducers_ReturnsExpectedContract_WhenHostAndAuthConfigured()
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = await CreateConfiguredClientAsync(settings);

        var url = settings.BuildProducersEndpointUrl(limit: 5, sortBy: "Name");
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CastCrewActorsResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.NotNull(payload.Items);
        Assert.True(payload.TotalRecordCount >= 0);
        Assert.Equal("Name", payload.SortBy);
    }

    private static async Task<HttpClient> CreateConfiguredClientAsync(IntegrationSettings settings)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };

        AddClientIdentityHeader(client);

        if (settings.HasApiKey)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Token", settings.ApiKey);
            return client;
        }

        var loginPayload = new
        {
            Username = settings.Username,
            Pw = settings.Password
        };

        using var loginResponse = await client.PostAsJsonAsync("Users/AuthenticateByName", loginPayload);
        if (!loginResponse.IsSuccessStatusCode)
        {
            throw new XunitException(
                $"Jellyfin login failed with status {(int)loginResponse.StatusCode}. " +
                "Check CASTCREW_USERNAME/CASTCREW_PASSWORD.");
        }

        var auth = await loginResponse.Content.ReadFromJsonAsync<JellyfinAuthenticateResponse>(JsonOptions);
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            throw new XunitException("Jellyfin authentication did not return an access token.");
        }

        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Token", auth.AccessToken);

        if (!string.IsNullOrWhiteSpace(auth.User?.Id) && string.IsNullOrWhiteSpace(settings.UserId))
        {
            settings.UserId = auth.User.Id;
        }

        return client;
    }

    private static void AddClientIdentityHeader(HttpClient client)
    {
        var authorizationHeader =
            "MediaBrowser Client=\"CastCrewIntegrationTests\", " +
            "Device=\"Copilot CLI\", DeviceId=\"castcrew-integration-tests\", Version=\"1.0.0\"";

        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Authorization", authorizationHeader);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private sealed class IntegrationSettings
    {
        public string BaseUrl { get; private set; } = "http://127.0.0.1:8096";

        public string? ApiKey { get; private set; }

        public string? Username { get; private set; }

        public string? Password { get; private set; }

        public string? UserId { get; set; }

        public bool ShouldRunIntegrationTests { get; private set; }

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public bool HasUsernamePassword =>
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);

        public static IntegrationSettings FromEnvironment()
        {
            var settings = new IntegrationSettings
            {
                ApiKey = Environment.GetEnvironmentVariable("CASTCREW_API_KEY"),
                Username = Environment.GetEnvironmentVariable("CASTCREW_USERNAME"),
                Password = Environment.GetEnvironmentVariable("CASTCREW_PASSWORD"),
                UserId = Environment.GetEnvironmentVariable("CASTCREW_USER_ID")
            };

            var configuredBaseUrl = Environment.GetEnvironmentVariable("CASTCREW_BASE_URL");
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                settings.BaseUrl = configuredBaseUrl.TrimEnd('/') + "/";
            }

            settings.ShouldRunIntegrationTests =
                string.Equals(
                    Environment.GetEnvironmentVariable("CASTCREW_RUN_INTEGRATION_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);

            return settings;
        }

        public string BuildActorsEndpointUrl(int limit, string sortBy)
        {
            return BuildEndpointUrl("Actors", limit, sortBy);
        }

        public string BuildDirectorsEndpointUrl(int limit, string sortBy)
        {
            return BuildEndpointUrl("Directors", limit, sortBy);
        }

        public string BuildProducersEndpointUrl(int limit, string sortBy)
        {
            return BuildEndpointUrl("Producers", limit, sortBy);
        }

        private string BuildEndpointUrl(string personType, int limit, string sortBy)
        {
            var query = $"CastCrew/{personType}?startIndex=0&limit={limit}&sortBy={Uri.EscapeDataString(sortBy)}";
            if (!string.IsNullOrWhiteSpace(UserId))
            {
                query += $"&userId={Uri.EscapeDataString(UserId)}";
            }

            return query;
        }
    }

    private sealed class CastCrewActorsResponse
    {
        public List<JsonElement> Items { get; set; } = new();

        public int TotalRecordCount { get; set; }

        public int StartIndex { get; set; }

        public int PageSize { get; set; }

        public string SortBy { get; set; } = string.Empty;

        public string DetailRoutePreference { get; set; } = string.Empty;
    }

    private sealed class JellyfinAuthenticateResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public JellyfinUserInfo? User { get; set; }
    }

    private sealed class JellyfinUserInfo
    {
        public string Id { get; set; } = string.Empty;
    }
}
