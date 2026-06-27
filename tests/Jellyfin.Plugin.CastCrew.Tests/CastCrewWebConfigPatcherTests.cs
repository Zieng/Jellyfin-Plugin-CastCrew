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

    [Fact]
    public void TryConfigureUserWebDirFallback_WindowsMode_CopiesWebRoot_AndSetsUserEnvVar()
    {
        var sourceWebRoot = CreateTemporaryWebRoot();
        var localAppDataRoot = Path.Combine(
            Path.GetTempPath(),
            "castcrew-localapp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppDataRoot);

        try
        {
            var nestedDir = Path.Combine(sourceWebRoot, "nested");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(sourceWebRoot, "config.json"), "{ \"menuLinks\": [] }");
            File.WriteAllText(Path.Combine(sourceWebRoot, "index.html"), "<html></html>");
            File.WriteAllText(Path.Combine(nestedDir, "child.txt"), "value");

            string? envName = null;
            string? envValue = null;
            EnvironmentVariableTarget? envTarget = null;

            void SetEnvVar(string name, string value, EnvironmentVariableTarget target)
            {
                envName = name;
                envValue = value;
                envTarget = target;
            }

            var result = InvokeTryConfigureUserWebDirFallback(
                sourceWebRoot,
                isWindows: true,
                localAppDataRoot,
                SetEnvVar);

            Assert.True(result);

            var expectedTargetWebRoot = Path.Combine(localAppDataRoot, "Jellyfin", "custom-web");
            Assert.True(File.Exists(Path.Combine(expectedTargetWebRoot, "config.json")));
            Assert.True(File.Exists(Path.Combine(expectedTargetWebRoot, "index.html")));
            Assert.True(File.Exists(Path.Combine(expectedTargetWebRoot, "nested", "child.txt")));

            Assert.Equal("JELLYFIN_WEB_DIR", envName);
            Assert.Equal(expectedTargetWebRoot, envValue);
            Assert.Equal(EnvironmentVariableTarget.User, envTarget);
        }
        finally
        {
            Directory.Delete(sourceWebRoot, recursive: true);
            Directory.Delete(localAppDataRoot, recursive: true);
        }
    }

    [Fact]
    public void TryConfigureUserWebDirFallback_WindowsMode_InvokesTrayRefreshWithFallbackPath()
    {
        var sourceWebRoot = CreateTemporaryWebRoot();
        var localAppDataRoot = Path.Combine(
            Path.GetTempPath(),
            "castcrew-localapp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppDataRoot);

        try
        {
            File.WriteAllText(Path.Combine(sourceWebRoot, "config.json"), "{ \"menuLinks\": [] }");
            File.WriteAllText(Path.Combine(sourceWebRoot, "index.html"), "<html></html>");

            string? refreshedPath = null;

            void SetEnvVar(string name, string value, EnvironmentVariableTarget target)
            {
            }

            void RefreshTray(string path)
            {
                refreshedPath = path;
            }

            var result = InvokeTryConfigureUserWebDirFallbackWithTrayRefresh(
                sourceWebRoot,
                isWindows: true,
                localAppDataRoot,
                SetEnvVar,
                RefreshTray);

            Assert.True(result);
            Assert.Equal(Path.Combine(localAppDataRoot, "Jellyfin", "custom-web"), refreshedPath);
        }
        finally
        {
            Directory.Delete(sourceWebRoot, recursive: true);
            Directory.Delete(localAppDataRoot, recursive: true);
        }
    }

    [Fact]
    public void TryConfigureUserWebDirFallback_WindowsMode_AlreadyConfigured_StillInvokesTrayRefresh()
    {
        var sourceWebRoot = CreateTemporaryWebRoot();
        var localAppDataRoot = Path.Combine(
            Path.GetTempPath(),
            "castcrew-localapp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppDataRoot);

        var fallbackWebRoot = Path.Combine(localAppDataRoot, "Jellyfin", "custom-web");
        Directory.CreateDirectory(fallbackWebRoot);
        File.WriteAllText(Path.Combine(fallbackWebRoot, "config.json"), "{ \"menuLinks\": [] }");
        File.WriteAllText(Path.Combine(fallbackWebRoot, "index.html"), "<html></html>");

        try
        {
            string? refreshedPath = null;
            var expectedFallbackWebRoot = Path.Combine(localAppDataRoot, "Jellyfin", "custom-web");

            void SetEnvVar(string name, string value, EnvironmentVariableTarget target)
            {
            }

            void RefreshTray(string path)
            {
                refreshedPath = path;
            }

            var result = InvokeTryConfigureUserWebDirFallbackWithTrayRefreshAndConfiguredValue(
                sourceWebRoot,
                isWindows: true,
                localAppDataRoot,
                SetEnvVar,
                RefreshTray,
                expectedFallbackWebRoot);

            Assert.True(result);
            Assert.Equal(expectedFallbackWebRoot, refreshedPath);
        }
        finally
        {
            Directory.Delete(sourceWebRoot, recursive: true);
            Directory.Delete(localAppDataRoot, recursive: true);
        }
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

    private static bool InvokeTryConfigureUserWebDirFallback(
        string sourceWebRoot,
        bool isWindows,
        string localAppDataRoot,
        Action<string, string, EnvironmentVariableTarget> setEnvVar)
    {
        var assembly = typeof(CastCrewPlugin).Assembly;
        var type = assembly.GetType("Jellyfin.Plugin.CastCrew.CastCrewWebConfigPatcher", throwOnError: true);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "TryConfigureUserWebDirFallback",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object?[] { sourceWebRoot, isWindows, null, localAppDataRoot, setEnvVar, null, null });
        Assert.NotNull(result);
        return (bool)result!;
    }

    private static bool InvokeTryConfigureUserWebDirFallbackWithTrayRefresh(
        string sourceWebRoot,
        bool isWindows,
        string localAppDataRoot,
        Action<string, string, EnvironmentVariableTarget> setEnvVar,
        Action<string> refreshTray,
        Func<string, EnvironmentVariableTarget, string?>? readEnvVar = null)
    {
        var assembly = typeof(CastCrewPlugin).Assembly;
        var type = assembly.GetType("Jellyfin.Plugin.CastCrew.CastCrewWebConfigPatcher", throwOnError: true);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "TryConfigureUserWebDirFallback",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object?[] { sourceWebRoot, isWindows, null, localAppDataRoot, setEnvVar, refreshTray, readEnvVar });
        Assert.NotNull(result);
        return (bool)result!;
    }

    private static bool InvokeTryConfigureUserWebDirFallbackWithTrayRefreshAndConfiguredValue(
        string sourceWebRoot,
        bool isWindows,
        string localAppDataRoot,
        Action<string, string, EnvironmentVariableTarget> setEnvVar,
        Action<string> refreshTray,
        string configuredValue)
    {
        string? ReadEnvVar(string name, EnvironmentVariableTarget _)
        {
            if (string.Equals(name, "JELLYFIN_WEB_DIR", StringComparison.Ordinal))
            {
                return configuredValue;
            }

            return null;
        }

        return InvokeTryConfigureUserWebDirFallbackWithTrayRefresh(
            sourceWebRoot,
            isWindows,
            localAppDataRoot,
            setEnvVar,
            refreshTray,
            ReadEnvVar);
    }
}
