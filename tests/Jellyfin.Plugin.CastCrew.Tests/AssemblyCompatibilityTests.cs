using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests;

/// <summary>
/// Validates that the compiled plugin assembly only references baseline Jellyfin SDK
/// versions, ensuring forward compatibility with all servers in the target series.
/// </summary>
/// <remarks>
/// The plugin must compile against the minimum targetAbi version (e.g., 10.11.0 for
/// Jellyfin 10.11.x) so that the assembly version references are satisfied by any
/// server in that series. Compiling against a newer SDK (e.g., 10.11.11) produces
/// references that cannot be loaded on older servers (e.g., 10.11.10), causing
/// ReflectionTypeLoadException at plugin load time.
/// </remarks>
public class AssemblyCompatibilityTests
{
    private static readonly Assembly PluginAssembly =
        typeof(CastCrewPlugin).Assembly;

    /// <summary>
    /// The maximum allowed assembly version for MediaBrowser references per target
    /// framework. This must match the baseline targetAbi for each Jellyfin series.
    /// </summary>
#if NET9_0_OR_GREATER
    private static readonly Version MaxAllowedMediaBrowserVersion = new Version(10, 11, 0, 0);
#else
    private static readonly Version MaxAllowedMediaBrowserVersion = new Version(10, 10, 0, 0);
#endif

    [Fact]
    public void PluginAssembly_ReferencesBaselineMediaBrowserVersions()
    {
        var mediaRefs = PluginAssembly.GetReferencedAssemblies()
            .Where(a => a.Name != null && a.Name.StartsWith("MediaBrowser", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(mediaRefs);

        foreach (var assemblyRef in mediaRefs)
        {
            Assert.True(
                assemblyRef.Version <= MaxAllowedMediaBrowserVersion,
                $"Assembly '{assemblyRef.Name}' references version {assemblyRef.Version}, " +
                $"which exceeds the baseline {MaxAllowedMediaBrowserVersion}. " +
                $"Compile against the minimum JellyfinVersion to ensure compatibility " +
                $"with all servers in the target series.");
        }
    }

    [Fact]
    public void PluginAssembly_ReferencesAllRequiredMediaBrowserAssemblies()
    {
        // Ensure the plugin references the expected MediaBrowser assemblies —
        // this guards against accidental removal of necessary references.
        var mediaRefNames = PluginAssembly.GetReferencedAssemblies()
            .Where(a => a.Name != null && a.Name.StartsWith("MediaBrowser", StringComparison.Ordinal))
            .Select(a => a.Name!)
            .ToHashSet();

        Assert.Contains("MediaBrowser.Common", mediaRefNames);
        Assert.Contains("MediaBrowser.Controller", mediaRefNames);
        Assert.Contains("MediaBrowser.Model", mediaRefNames);
    }

    [Fact]
    public void PluginAssembly_MediaBrowserVersionsAreConsistent()
    {
        // All MediaBrowser references should target the same baseline version.
        var mediaRefs = PluginAssembly.GetReferencedAssemblies()
            .Where(a => a.Name != null && a.Name.StartsWith("MediaBrowser", StringComparison.Ordinal))
            .ToList();

        var distinctVersions = mediaRefs
            .Select(a => a.Version)
            .Distinct()
            .ToList();

        Assert.True(
            distinctVersions.Count == 1,
            $"MediaBrowser references have inconsistent versions: " +
            $"{string.Join(", ", mediaRefs.Select(a => $"{a.Name} v{a.Version}"))}. " +
            $"All should target the same baseline.");
    }
}
