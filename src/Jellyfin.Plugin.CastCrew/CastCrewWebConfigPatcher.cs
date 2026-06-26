using System.Text.Json;
using System.Text.Json.Nodes;

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

    public static void SyncCastCrewMenuLink(string? webPath, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            Console.Error.WriteLine("[CastCrew] Unable to sync top banner link: WebPath is empty.");
            return;
        }

        if (!Directory.Exists(webPath))
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: WebPath '{webPath}' does not exist.");
            return;
        }

        var configPath = Path.Combine(webPath, ConfigFileName);
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: '{configPath}' was not found.");
            return;
        }

        try
        {
            var configText = File.ReadAllText(configPath);
            var root = JsonNode.Parse(configText) as JsonObject;
            if (root is null)
            {
                Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: '{configPath}' is not a valid JSON object.");
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
                if (!EnsureCastCrewStandalonePageFile(webPath))
                {
                    return;
                }

                if (!EnsureTopBannerScriptFile(webPath))
                {
                    return;
                }

                if (!SyncTopBannerScriptTag(webPath, enabled: true))
                {
                    return;
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
                if (!SyncTopBannerScriptTag(webPath, enabled: false))
                {
                    return;
                }

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
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: I/O failure. {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: access denied. {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top banner link: invalid JSON. {ex.Message}");
        }
    }

    private static string BuildCastCrewPageUrl()
        => CastCrewHomeTabRoute;

    private static bool EnsureCastCrewStandalonePageFile(string webPath)
        => EnsureEmbeddedAssetFile(
            webPath,
            CastCrewStandaloneFileName,
            CastCrewStandaloneResourceName,
            "standalone cast & crew page");

    private static bool EnsureTopBannerScriptFile(string webPath)
        => EnsureEmbeddedAssetFile(
            webPath,
            TopBannerScriptFileName,
            TopBannerScriptResourceName,
            "top-banner cast & crew route script");

    private static bool EnsureEmbeddedAssetFile(
        string webPath,
        string outputFileName,
        string resourceName,
        string assetDescription)
    {
        var outputPath = Path.Combine(webPath, outputFileName);
        using var resourceStream = typeof(CastCrewWebConfigPatcher).Assembly
            .GetManifestResourceStream(resourceName);

        if (resourceStream is null)
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync {assetDescription}: embedded resource '{resourceName}' was not found.");
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

        File.WriteAllText(outputPath, outputContent);
        return true;
    }

    private static bool SyncTopBannerScriptTag(string webPath, bool enabled)
    {
        var indexPath = Path.Combine(webPath, IndexFileName);
        if (!File.Exists(indexPath))
        {
            Console.Error.WriteLine($"[CastCrew] Unable to sync top-banner script tag: '{indexPath}' was not found.");
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
                Console.Error.WriteLine($"[CastCrew] Unable to sync top-banner script tag: '{indexPath}' has no </head> or </body> marker.");
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
            File.WriteAllText(indexPath, updatedContent);
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
}
