using System.Reflection;
using System.Text.Json.Nodes;
using Jellyfin.Plugin.CastCrew;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests;

public sealed class CastCrewWebConfigPatcherTests
{
    private const string CastCrewLinkName = "Cast&Crew";
    private const string CastCrewHomeTabUrl = "/web/#/home?tab=cast_crew";
    private const string StandaloneCastCrewUrl = "/web/cast-crew.html";
    private const string TopBannerScriptTag = "<script src=\"castcrew-top-banner-link.js\" defer></script>";

    [Fact]
    public void SyncCastCrewMenuLink_Enabled_MigratesCastCrewLinkToHomeTabRoute_AndEnsuresScriptTag()
    {
        var webRoot = CreateTemporaryWebRoot();
        try
        {
            var configPath = Path.Combine(webRoot, "config.json");
            File.WriteAllText(
                configPath,
                """
                {
                  "menuLinks": [
                    {
                      "name": "Home",
                      "icon": "home",
                      "url": "/web/#/home"
                    },
                    {
                      "name": "Cast&Crew",
                      "icon": "person",
                      "url": "/web/cast-crew.html"
                    }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(webRoot, "index.html"),
                "<!DOCTYPE html><html><head></head><body></body></html>");

            InvokeSyncCastCrewMenuLink(webRoot, enabled: true);

            var menuLinks = ReadMenuLinks(configPath);
            var castCrewLink = FindCastCrewLink(menuLinks);
            Assert.NotNull(castCrewLink);
            Assert.Equal(CastCrewLinkName, ReadString(castCrewLink!, "name"));
            Assert.Equal("person", ReadString(castCrewLink!, "icon"));
            Assert.Equal(CastCrewHomeTabUrl, ReadString(castCrewLink!, "url"));

            var indexContent = File.ReadAllText(Path.Combine(webRoot, "index.html"));
            Assert.Contains(TopBannerScriptTag, indexContent, StringComparison.Ordinal);

            var standalonePagePath = Path.Combine(webRoot, "cast-crew.html");
            Assert.True(File.Exists(standalonePagePath));
            var standalonePageContent = File.ReadAllText(standalonePagePath);
            Assert.Contains("/web/#/home?tab=cast_crew", standalonePageContent, StringComparison.Ordinal);

            var scriptPath = Path.Combine(webRoot, "castcrew-top-banner-link.js");
            Assert.True(File.Exists(scriptPath));
            var scriptContent = File.ReadAllText(scriptPath);
            Assert.Contains("syncCastCrewRouteView", scriptContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void SyncCastCrewMenuLink_Disabled_RemovesCastCrewLink_AndRemovesLegacyScriptTag()
    {
        var webRoot = CreateTemporaryWebRoot();
        try
        {
            var configPath = Path.Combine(webRoot, "config.json");
            File.WriteAllText(
                configPath,
                """
                {
                  "menuLinks": [
                    {
                      "name": "Home",
                      "icon": "home",
                      "url": "/web/#/home"
                    },
                    {
                      "name": "Cast&Crew",
                      "icon": "person",
                      "url": "/web/cast-crew.html"
                    }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(webRoot, "index.html"),
                $$"""
                <!DOCTYPE html>
                <html>
                <head>
                    {{TopBannerScriptTag}}
                </head>
                <body></body>
                </html>
                """);

            InvokeSyncCastCrewMenuLink(webRoot, enabled: false);

            var menuLinks = ReadMenuLinks(configPath);
            Assert.Null(FindCastCrewLink(menuLinks));
            Assert.DoesNotContain(
                menuLinks,
                node => string.Equals(ReadString(node as JsonObject, "url"), CastCrewHomeTabUrl, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                menuLinks,
                node => string.Equals(ReadString(node as JsonObject, "url"), StandaloneCastCrewUrl, StringComparison.OrdinalIgnoreCase));

            var indexContent = File.ReadAllText(Path.Combine(webRoot, "index.html"));
            Assert.DoesNotContain(TopBannerScriptTag, indexContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void SyncCastCrewMenuLink_Enabled_MigratesStandaloneUrl_WhenNameDoesNotMatch()
    {
        var webRoot = CreateTemporaryWebRoot();
        try
        {
            var configPath = Path.Combine(webRoot, "config.json");
            File.WriteAllText(
                configPath,
                """
                {
                  "menuLinks": [
                    {
                      "name": "CastCrew Legacy",
                      "icon": "person",
                      "url": "/web/cast-crew.html"
                    }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(webRoot, "index.html"),
                "<!DOCTYPE html><html><head></head><body></body></html>");

            InvokeSyncCastCrewMenuLink(webRoot, enabled: true);

            var menuLinks = ReadMenuLinks(configPath);
            Assert.Single(menuLinks);
            var castCrewLink = FindCastCrewLink(menuLinks);
            Assert.NotNull(castCrewLink);
            Assert.Equal(CastCrewLinkName, ReadString(castCrewLink!, "name"));
            Assert.Equal(CastCrewHomeTabUrl, ReadString(castCrewLink!, "url"));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void SyncCastCrewMenuLink_Enabled_StillAddsMenuLink_WhenIndexHtmlIsMissing()
    {
        var webRoot = CreateTemporaryWebRoot();
        try
        {
            var configPath = Path.Combine(webRoot, "config.json");
            File.WriteAllText(
                configPath,
                """
                {
                  "menuLinks": []
                }
                """);

            // Intentionally no index.html to reproduce script-tag sync failure.
            InvokeSyncCastCrewMenuLink(webRoot, enabled: true);

            var menuLinks = ReadMenuLinks(configPath);
            var castCrewLink = FindCastCrewLink(menuLinks);
            Assert.NotNull(castCrewLink);
            Assert.Equal(CastCrewLinkName, ReadString(castCrewLink!, "name"));
            Assert.Equal(CastCrewHomeTabUrl, ReadString(castCrewLink!, "url"));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void SyncCastCrewMenuLink_WhenLoggerIsNull_WritesWarningToConsoleError()
    {
        var missingWebRoot = Path.Combine(
            Path.GetTempPath(),
            "castcrew-missing-" + Guid.NewGuid().ToString("N"));

        using var errorWriter = new StringWriter();
        var originalError = Console.Error;

        try
        {
            Console.SetError(errorWriter);

            InvokeSyncCastCrewMenuLink(missingWebRoot, enabled: true);

            var output = errorWriter.ToString();
            Assert.Contains("Unable to sync menu link", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(missingWebRoot, output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void SyncCastCrewMenuLink_MissingWebRoot_ReturnsFailureStatus()
    {
        var missingWebRoot = Path.Combine(
            Path.GetTempPath(),
            "castcrew-missing-status-" + Guid.NewGuid().ToString("N"));

        var result = InvokeSyncCastCrewMenuLink(missingWebRoot, enabled: true);
        Assert.NotNull(result);
        Assert.Equal("Failed", result!.ToString());
    }

    private static string CreateTemporaryWebRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "castcrew-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static JsonArray ReadMenuLinks(string configPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
        Assert.NotNull(root);
        var menuLinks = root!["menuLinks"] as JsonArray;
        Assert.NotNull(menuLinks);
        return menuLinks!;
    }

    private static JsonObject? FindCastCrewLink(JsonArray menuLinks)
    {
        foreach (var node in menuLinks)
        {
            if (node is not JsonObject link)
            {
                continue;
            }

            var name = ReadString(link, "name");
            var url = ReadString(link, "url");
            if (string.Equals(name, CastCrewLinkName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(url, CastCrewHomeTabUrl, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(url, StandaloneCastCrewUrl, StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }
        }

        return null;
    }

    private static string? ReadString(JsonObject? node, string key)
    {
        if (node is null)
        {
            return null;
        }

        var value = node[key];
        if (value is null)
        {
            return null;
        }

        return value.GetValue<string>();
    }

    private static object? InvokeSyncCastCrewMenuLink(string webRoot, bool enabled)
    {
        var assembly = typeof(CastCrewPlugin).Assembly;
        var type = assembly.GetType("Jellyfin.Plugin.CastCrew.CastCrewWebConfigPatcher", throwOnError: true);
        Assert.NotNull(type);
        var method = type!.GetMethod("SyncCastCrewMenuLink", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, new object?[] { webRoot, enabled, null });
    }
}
