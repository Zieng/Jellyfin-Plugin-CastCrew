using System.Security;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CastCrew;

internal static class CastCrewPluginManifestCompatibility
{
    private const string MetaFileName = "meta.json";

    internal static bool TryApplyReadOnlyManifestStartupWorkaround(
        IPluginManager? pluginManager,
        Guid pluginId,
        string assemblyPath,
        Version? assemblyVersion,
        Func<string, bool>? canWriteManifest = null,
        Action<string>? logWarning = null)
    {
        if (pluginManager is null ||
            assemblyVersion is null ||
            string.IsNullOrWhiteSpace(assemblyPath))
        {
            return false;
        }

        var plugin = FindPlugin(pluginManager, pluginId, assemblyPath);
        if (plugin is null ||
            plugin.Manifest.Status != PluginStatus.Active)
        {
            return false;
        }

        var assemblyVersionText = assemblyVersion.ToString();
        if (!string.Equals(plugin.Manifest.Version, assemblyVersionText, StringComparison.Ordinal))
        {
            return false;
        }

        var manifestPath = Path.Combine(plugin.Path, MetaFileName);
        var writeProbe = canWriteManifest ?? IsManifestWritable;
        if (writeProbe(manifestPath))
        {
            return false;
        }

        // Jellyfin compares manifest.Version via ordinal string equality and writes meta.json
        // when the value exactly matches assembly version. A trailing space keeps Version.Parse
        // valid while avoiding the forced write path for read-only plugin directories.
        plugin.Manifest.Version = assemblyVersionText + " ";
        logWarning?.Invoke(
            $"Detected read-only plugin manifest path '{manifestPath}'. Applied startup compatibility workaround for Jellyfin meta.json writes.");
        return true;
    }

    private static LocalPlugin? FindPlugin(IPluginManager pluginManager, Guid pluginId, string assemblyPath)
    {
        var assemblyFileName = Path.GetFileName(assemblyPath);
        if (string.IsNullOrWhiteSpace(assemblyFileName))
        {
            return null;
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var plugin in pluginManager.Plugins)
        {
            if (plugin.Id != pluginId)
            {
                continue;
            }

            var candidateAssemblyPath = Path.Combine(plugin.Path, assemblyFileName);
            if (string.Equals(candidateAssemblyPath, assemblyPath, pathComparison))
            {
                return plugin;
            }
        }

        return null;
    }

    private static bool IsManifestWritable(string manifestPath)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            Directory.CreateDirectory(directoryPath);
            using var stream = new FileStream(
                manifestPath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.Read);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
