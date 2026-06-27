using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

internal static class CastCrewWebConfigPatcher
{
    private const string CastCrewStandaloneFileName = "cast-crew.html";
    private const string CastCrewStandaloneResourceName = "Jellyfin.Plugin.CastCrew.Web.cast-crew-standalone.html";
    private const string TopBannerScriptFileName = "castcrew-top-banner-link.js";
    private const string TopBannerScriptResourceName = "Jellyfin.Plugin.CastCrew.Web.castcrew-top-banner-link.js";
    private const string CastCrewLinkName = "Cast&Crew";
    private const string CastCrewLinkIcon = "person";
    private const string CastCrewHomeTabRoute = "/web/#/home?tab=cast_crew";
    private const string CastCrewStandaloneRoute = "/web/cast-crew.html";
    private const string ConfigFileName = "config.json";
    private const string IndexFileName = "index.html";
    private const string TopBannerScriptTag = "<script src=\"castcrew-top-banner-link.js\" defer></script>";

    public static void SyncCastCrewMenuLink(string? webPath, bool enabled, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: WebPath is empty.");
            return;
        }

        if (!Directory.Exists(webPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: WebPath '{0}' does not exist.", webPath);
            return;
        }

        var configPath = Path.Combine(webPath, ConfigFileName);
        if (!File.Exists(configPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: '{0}' was not found.", configPath);
            return;
        }

        try
        {
            var configText = File.ReadAllText(configPath);
            var root = JsonNode.Parse(configText) as JsonObject;
            if (root is null)
            {
                Log(logger, LogLevel.Warning, "Unable to sync menu link: '{0}' is not a valid JSON object.", configPath);
                return;
            }

            var changed = false;
            var menuLinks = root["menuLinks"] as JsonArray;
            if (menuLinks is null)
            {
                menuLinks = new JsonArray();
                root["menuLinks"] = menuLinks;
                changed = true;
            }

            var castCrewLinkUrl = BuildCastCrewPageUrl();
            if (enabled)
            {
                // Attempt top-banner script injection but do NOT abort menu link
                // updates when it fails — the sidebar link is independent.
                var canSyncTopBannerScript =
                    EnsureCastCrewStandalonePageFile(webPath, logger) &&
                    EnsureTopBannerScriptFile(webPath, logger);

                if (canSyncTopBannerScript)
                {
                    SyncTopBannerScriptTag(webPath, enabled: true, logger);
                }

                var existingLink = FindCastCrewMenuLink(menuLinks, castCrewLinkUrl);
                if (existingLink is null)
                {
                    menuLinks.Add(new JsonObject
                    {
                        ["name"] = CastCrewLinkName,
                        ["icon"] = CastCrewLinkIcon,
                        ["url"] = castCrewLinkUrl
                    });

                    changed = true;
                }
                else
                {
                    changed |= SetString(existingLink, "name", CastCrewLinkName);
                    changed |= SetString(existingLink, "icon", CastCrewLinkIcon);
                    changed |= SetString(existingLink, "url", castCrewLinkUrl);
                }
            }
            else
            {
                SyncTopBannerScriptTag(webPath, enabled: false, logger);

                for (var index = menuLinks.Count - 1; index >= 0; index--)
                {
                    if (menuLinks[index] is JsonObject link &&
                        IsCastCrewMenuLink(link, castCrewLinkUrl))
                    {
                        menuLinks.RemoveAt(index);
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(
                configPath,
                root.ToJsonString(jsonOptions) + Environment.NewLine);

            Log(logger, LogLevel.Information, "Successfully synced Cast & Crew menu link (enabled={0}).", enabled);
        }
        catch (IOException ex)
        {
            Log(logger, LogLevel.Error, "Unable to sync menu link: I/O failure writing to web root. {0}", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log(logger, LogLevel.Error, "Unable to sync menu link: access denied to web root. On Windows, ensure Jellyfin has write access to the web directory. {0}", ex.Message);
        }
        catch (JsonException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: invalid JSON in config. {0}", ex.Message);
        }
    }

    private static string BuildCastCrewPageUrl()
        => CastCrewHomeTabRoute;

    private static bool EnsureCastCrewStandalonePageFile(string webPath, ILogger? logger)
        => EnsureEmbeddedAssetFile(
            webPath,
            CastCrewStandaloneFileName,
            CastCrewStandaloneResourceName,
            "standalone cast & crew page",
            logger);

    private static bool EnsureTopBannerScriptFile(string webPath, ILogger? logger)
        => EnsureEmbeddedAssetFile(
            webPath,
            TopBannerScriptFileName,
            TopBannerScriptResourceName,
            "top-banner cast & crew route script",
            logger);

    private static bool EnsureEmbeddedAssetFile(
        string webPath,
        string outputFileName,
        string resourceName,
        string assetDescription,
        ILogger? logger)
    {
        var outputPath = Path.Combine(webPath, outputFileName);
        using var resourceStream = typeof(CastCrewWebConfigPatcher).Assembly
            .GetManifestResourceStream(resourceName);

        if (resourceStream is null)
        {
            Log(logger, LogLevel.Warning, "Unable to sync {0}: embedded resource '{1}' was not found.", assetDescription, resourceName);
            return false;
        }

        using var resourceReader = new StreamReader(resourceStream);
        var outputContent = resourceReader.ReadToEnd();

        if (File.Exists(outputPath))
        {
            var existingContent = File.ReadAllText(outputPath);
            if (string.Equals(existingContent, outputContent, StringComparison.Ordinal))
            {
                return true;
            }
        }

        try
        {
            File.WriteAllText(outputPath, outputContent);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log(logger, LogLevel.Error, "Unable to write {0} to '{1}': access denied. {2}", assetDescription, outputPath, ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            Log(logger, LogLevel.Error, "Unable to write {0} to '{1}': I/O error. {2}", assetDescription, outputPath, ex.Message);
            return false;
        }

        return true;
    }

    private static bool SyncTopBannerScriptTag(string webPath, bool enabled, ILogger? logger)
    {
        var indexPath = Path.Combine(webPath, IndexFileName);
        if (!File.Exists(indexPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync top-banner script tag: '{0}' was not found.", indexPath);
            return false;
        }

        var indexContent = File.ReadAllText(indexPath);

        // Detect existing line ending style in the file
        var lineEnding = indexContent.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var containsScriptTag = indexContent.Contains(TopBannerScriptTag, StringComparison.Ordinal);
        if (enabled == containsScriptTag)
        {
            return true;
        }

        string updatedContent;
        if (enabled)
        {
            var insertionIndex = indexContent.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (insertionIndex < 0)
            {
                insertionIndex = indexContent.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            }

            if (insertionIndex < 0)
            {
                Log(logger, LogLevel.Warning, "Unable to sync top-banner script tag: '{0}' has no </head> or </body> marker.", indexPath);
                return false;
            }

            var scriptTagLine = $"    {TopBannerScriptTag}{lineEnding}";
            updatedContent = indexContent.Insert(insertionIndex, scriptTagLine);
        }
        else
        {
            updatedContent = indexContent
                .Replace($"    {TopBannerScriptTag}\r\n", string.Empty, StringComparison.Ordinal)
                .Replace($"    {TopBannerScriptTag}\n", string.Empty, StringComparison.Ordinal)
                .Replace($"{TopBannerScriptTag}\r\n", string.Empty, StringComparison.Ordinal)
                .Replace($"{TopBannerScriptTag}\n", string.Empty, StringComparison.Ordinal)
                .Replace(TopBannerScriptTag, string.Empty, StringComparison.Ordinal);
        }

        if (!string.Equals(indexContent, updatedContent, StringComparison.Ordinal))
        {
            try
            {
                File.WriteAllText(indexPath, updatedContent);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log(logger, LogLevel.Error, "Unable to modify '{0}': access denied. {1}", indexPath, ex.Message);
                return false;
            }
            catch (IOException ex)
            {
                Log(logger, LogLevel.Error, "Unable to modify '{0}': I/O error. {1}", indexPath, ex.Message);
                return false;
            }
        }

        return true;
    }

    private static JsonObject? FindCastCrewMenuLink(JsonArray menuLinks, string castCrewLinkUrl)
    {
        foreach (var node in menuLinks)
        {
            if (node is JsonObject link && IsCastCrewMenuLink(link, castCrewLinkUrl))
            {
                return link;
            }
        }

        return null;
    }

    private static bool IsCastCrewMenuLink(JsonObject link, string castCrewLinkUrl)
    {
        var name = ReadString(link, "name");
        var url = ReadString(link, "url");

        return string.Equals(name, CastCrewLinkName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(url, castCrewLinkUrl, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(url, CastCrewStandaloneRoute, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonObject link, string key)
    {
        var node = link[key];
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return node.ToString();
    }

    private static bool SetString(JsonObject link, string key, string value)
    {
        var currentValue = ReadString(link, key);
        if (string.Equals(currentValue, value, StringComparison.Ordinal))
        {
            return false;
        }

        link[key] = value;
        return true;
    }

    private static void Log(ILogger? logger, LogLevel level, string message, params object[] args)
    {
        if (logger is not null)
        {
            logger.Log(level, "[CastCrew] " + message, args);
        }
    }
}
