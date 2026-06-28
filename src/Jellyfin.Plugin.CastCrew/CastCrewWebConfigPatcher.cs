using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CastCrew;

internal enum CastCrewWebConfigSyncStatus
{
    Succeeded,
    FailedNeedsWritableWebRoot,
    Failed
}

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
    private const string JellyfinWebDirEnvVar = "JELLYFIN_WEB_DIR";

    /// <summary>
    /// Tracks the most recent sync status so that fallback page registration
    /// can be determined without re-running the sync operation.
    /// </summary>
    internal static CastCrewWebConfigSyncStatus LastSyncStatus { get; private set; } = CastCrewWebConfigSyncStatus.Failed;

    public static CastCrewWebConfigSyncStatus SyncCastCrewMenuLink(string? webPath, bool enabled, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(webPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: WebPath is empty.");
            LastSyncStatus = CastCrewWebConfigSyncStatus.Failed;
            return LastSyncStatus;
        }

        if (!Directory.Exists(webPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: WebPath '{0}' does not exist.", webPath);
            LastSyncStatus = CastCrewWebConfigSyncStatus.Failed;
            return LastSyncStatus;
        }

        var configPath = Path.Combine(webPath, ConfigFileName);
        if (!File.Exists(configPath))
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: '{0}' was not found.", configPath);
            LastSyncStatus = CastCrewWebConfigSyncStatus.Failed;
            return LastSyncStatus;
        }

        try
        {
            var configText = File.ReadAllText(configPath);
            var root = JsonNode.Parse(configText) as JsonObject;
            if (root is null)
            {
                Log(logger, LogLevel.Warning, "Unable to sync menu link: '{0}' is not a valid JSON object.", configPath);
                return CastCrewWebConfigSyncStatus.Failed;
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
                LastSyncStatus = CastCrewWebConfigSyncStatus.Succeeded;
                return LastSyncStatus;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(
                configPath,
                root.ToJsonString(jsonOptions) + Environment.NewLine);

            Log(logger, LogLevel.Information, "Successfully synced Cast & Crew menu link (enabled={0}).", enabled);
            LastSyncStatus = CastCrewWebConfigSyncStatus.Succeeded;
            return LastSyncStatus;
        }
        catch (IOException ex)
        {
            Log(logger, LogLevel.Error, "Unable to sync menu link: I/O failure writing to web root. {0}", ex.Message);
            LastSyncStatus = CastCrewWebConfigSyncStatus.Failed;
            return LastSyncStatus;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log(logger, LogLevel.Error, "Unable to sync menu link: access denied to web root. " +
                "This commonly occurs in Docker containers or installer-based deployments where the web root is read-only. " +
                "To fix: mount the web directory as a writable volume, use a writable --webdir, or grant Jellyfin write access. " +
                "The plugin will fall back to a built-in Cast & Crew page. {0}", ex.Message);

            var configuredFallback = TryConfigureUserWebDirFallback(
                webPath,
                OperatingSystem.IsWindows(),
                logger,
                null,
                Environment.SetEnvironmentVariable,
                fallbackWebRoot => TryRestartWindowsTrayWithWebDir(fallbackWebRoot, logger),
                null);

            if (configuredFallback)
            {
                Log(
                    logger,
                    LogLevel.Warning,
                    "Configured user-level '{0}' fallback for writable Jellyfin web assets. Restart Jellyfin to apply.",
                    JellyfinWebDirEnvVar);
            }

            LastSyncStatus = CastCrewWebConfigSyncStatus.FailedNeedsWritableWebRoot;
            return LastSyncStatus;
        }
        catch (JsonException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to sync menu link: invalid JSON in config. {0}", ex.Message);
            LastSyncStatus = CastCrewWebConfigSyncStatus.Failed;
            return LastSyncStatus;
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

    private static bool TryConfigureUserWebDirFallback(
        string sourceWebRoot,
        bool isWindows,
        ILogger? logger,
        string? localAppDataRoot,
        Action<string, string, EnvironmentVariableTarget> setEnvironmentVariable,
        Action<string>? refreshTrayWithWebDir,
        Func<string, EnvironmentVariableTarget, string?>? getEnvironmentVariable)
    {
        if (!isWindows || string.IsNullOrWhiteSpace(sourceWebRoot) || !Directory.Exists(sourceWebRoot))
        {
            return false;
        }

        var localAppData = localAppDataRoot;
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            Log(logger, LogLevel.Warning, "Unable to configure writable web root fallback: LocalApplicationData path is empty.");
            return false;
        }

        var fallbackWebRoot = Path.Combine(localAppData, "Jellyfin", "custom-web");
        var fallbackConfigPath = Path.Combine(fallbackWebRoot, ConfigFileName);
        var fallbackIndexPath = Path.Combine(fallbackWebRoot, IndexFileName);

        try
        {
            var readEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
            var configuredWebDir = readEnvironmentVariable(JellyfinWebDirEnvVar, EnvironmentVariableTarget.User);
            if (string.Equals(configuredWebDir, fallbackWebRoot, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(fallbackConfigPath) &&
                File.Exists(fallbackIndexPath))
            {
                refreshTrayWithWebDir?.Invoke(fallbackWebRoot);
                return true;
            }

            MirrorDirectory(sourceWebRoot, fallbackWebRoot);
            setEnvironmentVariable(JellyfinWebDirEnvVar, fallbackWebRoot, EnvironmentVariableTarget.User);
            NotifyEnvironmentChanged(logger);
            refreshTrayWithWebDir?.Invoke(fallbackWebRoot);
            return true;
        }
        catch (ArgumentException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to configure writable web root fallback: invalid path. {0}", ex.Message);
            return false;
        }
        catch (SecurityException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to configure writable web root fallback: security restriction. {0}", ex.Message);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to configure writable web root fallback: access denied. {0}", ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to configure writable web root fallback: I/O failure. {0}", ex.Message);
            return false;
        }
    }

    private static void TryRestartWindowsTrayWithWebDir(string fallbackWebRoot, ILogger? logger)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(fallbackWebRoot))
        {
            return;
        }

        foreach (var trayProcess in Process.GetProcessesByName("Jellyfin.Windows.Tray"))
        {
            using (trayProcess)
            {
                string? trayExecutablePath;
                try
                {
                    trayExecutablePath = trayProcess.MainModule?.FileName;
                }
                catch (InvalidOperationException ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to inspect Jellyfin tray process before restart. {0}", ex.Message);
                    continue;
                }
                catch (Win32Exception ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to inspect Jellyfin tray process before restart. {0}", ex.Message);
                    continue;
                }
                catch (NotSupportedException ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to inspect Jellyfin tray process before restart. {0}", ex.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trayExecutablePath) || !File.Exists(trayExecutablePath))
                {
                    continue;
                }

                try
                {
                    trayProcess.Kill(entireProcessTree: false);
                    trayProcess.WaitForExit(5000);
                }
                catch (InvalidOperationException ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to restart Jellyfin tray process. {0}", ex.Message);
                    continue;
                }
                catch (NotSupportedException ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to restart Jellyfin tray process. {0}", ex.Message);
                    continue;
                }
                catch (Win32Exception ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to restart Jellyfin tray process. {0}", ex.Message);
                    continue;
                }

                var startInfo = new ProcessStartInfo(trayExecutablePath)
                {
                    UseShellExecute = false,
                };
                startInfo.Environment[JellyfinWebDirEnvVar] = fallbackWebRoot;

                try
                {
                    _ = Process.Start(startInfo);
                    Log(logger, LogLevel.Information, "Restarted Jellyfin tray process with '{0}' for future server restarts.", JellyfinWebDirEnvVar);
                }
                catch (InvalidOperationException ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to launch Jellyfin tray process after restart. {0}", ex.Message);
                }
                catch (Win32Exception ex)
                {
                    Log(logger, LogLevel.Warning, "Unable to launch Jellyfin tray process after restart. {0}", ex.Message);
                }

                return;
            }
        }
    }

    private static void MirrorDirectory(string sourceRoot, string targetRoot)
    {
        Directory.CreateDirectory(targetRoot);

        foreach (var sourceDirectory in Directory.GetDirectories(sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var directoryName = Path.GetFileName(sourceDirectory);
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                continue;
            }

            var targetDirectory = Path.Combine(targetRoot, directoryName);
            MirrorDirectory(sourceDirectory, targetDirectory);
        }

        foreach (var sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(sourceFile);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var targetFile = Path.Combine(targetRoot, fileName);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    private static void NotifyEnvironmentChanged(ILogger? logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            SendMessageTimeout(
                new IntPtr(0xFFFF),
                0x001A,
                IntPtr.Zero,
                "Environment",
                0x0002,
                2000,
                out _);
        }
        catch (EntryPointNotFoundException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to broadcast environment change notification. {0}", ex.Message);
        }
        catch (DllNotFoundException ex)
        {
            Log(logger, LogLevel.Warning, "Unable to broadcast environment change notification. {0}", ex.Message);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    private static void Log(ILogger? logger, LogLevel level, string message, params object[] args)
    {
        if (logger is not null)
        {
            logger.Log(level, "[CastCrew] " + message, args);
            return;
        }

        if (args.Length > 0)
        {
            Console.Error.WriteLine("[CastCrew] " + string.Format(CultureInfo.InvariantCulture, message, args));
        }
        else
        {
            Console.Error.WriteLine("[CastCrew] " + message);
        }
    }
}
