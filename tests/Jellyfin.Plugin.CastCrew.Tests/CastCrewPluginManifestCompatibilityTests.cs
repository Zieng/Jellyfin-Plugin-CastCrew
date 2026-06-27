using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Updates;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests;

public sealed class CastCrewPluginManifestCompatibilityTests
{
    [Fact]
    public void TryApplyReadOnlyManifestStartupWorkaround_ReadOnlyManifest_UsesCompatibilityVersionString()
    {
        var pluginId = Guid.Parse("a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a");
        var assemblyVersion = new Version(0, 1, 0, 4);
        var assemblyPath = @"C:\plugins\CastCrew_0.1.0.4\Jellyfin.Plugin.CastCrew.dll";
        var localPlugin = CreateLocalPlugin(pluginId, assemblyVersion.ToString(), PluginStatus.Active, assemblyPath);
        var pluginManager = new TestPluginManager(localPlugin);

        var result = InvokeTryApplyReadOnlyManifestStartupWorkaround(
            pluginManager,
            pluginId,
            assemblyPath,
            assemblyVersion,
            _ => false);

        Assert.True(result);
        Assert.Equal("0.1.0.4 ", localPlugin.Manifest.Version);
    }

    [Fact]
    public void TryApplyReadOnlyManifestStartupWorkaround_WritableManifest_DoesNotMutateManifestVersion()
    {
        var pluginId = Guid.Parse("a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a");
        var assemblyVersion = new Version(0, 1, 0, 4);
        var assemblyPath = @"C:\plugins\CastCrew_0.1.0.4\Jellyfin.Plugin.CastCrew.dll";
        var localPlugin = CreateLocalPlugin(pluginId, assemblyVersion.ToString(), PluginStatus.Active, assemblyPath);
        var pluginManager = new TestPluginManager(localPlugin);

        var result = InvokeTryApplyReadOnlyManifestStartupWorkaround(
            pluginManager,
            pluginId,
            assemblyPath,
            assemblyVersion,
            _ => true);

        Assert.False(result);
        Assert.Equal("0.1.0.4", localPlugin.Manifest.Version);
    }

    [Fact]
    public void TryApplyReadOnlyManifestStartupWorkaround_NonActiveStatus_DoesNotMutateManifestVersion()
    {
        var pluginId = Guid.Parse("a1c3e5f7-2b4d-6e8f-0a1c-3e5f7b9d1e3a");
        var assemblyVersion = new Version(0, 1, 0, 4);
        var assemblyPath = @"C:\plugins\CastCrew_0.1.0.4\Jellyfin.Plugin.CastCrew.dll";
        var localPlugin = CreateLocalPlugin(pluginId, assemblyVersion.ToString(), PluginStatus.Disabled, assemblyPath);
        var pluginManager = new TestPluginManager(localPlugin);

        var result = InvokeTryApplyReadOnlyManifestStartupWorkaround(
            pluginManager,
            pluginId,
            assemblyPath,
            assemblyVersion,
            _ => false);

        Assert.False(result);
        Assert.Equal("0.1.0.4", localPlugin.Manifest.Version);
    }

    private static LocalPlugin CreateLocalPlugin(Guid pluginId, string version, PluginStatus status, string assemblyPath)
    {
        var pluginPath = Path.GetDirectoryName(assemblyPath)
            ?? throw new InvalidOperationException("Assembly path should contain a directory.");

        var manifest = new PluginManifest
        {
            Id = pluginId,
            Name = "CastCrew",
            Description = "Test manifest",
            Version = version,
            Status = status,
            TargetAbi = "10.11.0.0"
        };

        return new LocalPlugin(pluginPath, isSupported: true, manifest);
    }

    private static bool InvokeTryApplyReadOnlyManifestStartupWorkaround(
        IPluginManager pluginManager,
        Guid pluginId,
        string assemblyPath,
        Version assemblyVersion,
        Func<string, bool> canWriteManifest)
    {
        var assembly = typeof(CastCrewPlugin).Assembly;
        var type = assembly.GetType("Jellyfin.Plugin.CastCrew.CastCrewPluginManifestCompatibility", throwOnError: true);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "TryApplyReadOnlyManifestStartupWorkaround",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(
            null,
            new object?[]
            {
                pluginManager,
                pluginId,
                assemblyPath,
                assemblyVersion,
                canWriteManifest,
                null
            });
        Assert.NotNull(result);
        return (bool)result!;
    }

    private sealed class TestPluginManager : IPluginManager
    {
        private readonly IReadOnlyList<LocalPlugin> _plugins;

        public TestPluginManager(params LocalPlugin[] plugins)
        {
            _plugins = plugins;
        }

        public IReadOnlyList<LocalPlugin> Plugins => _plugins;

        public void CreatePlugins()
        {
        }

        public IEnumerable<System.Reflection.Assembly> LoadAssemblies() => Array.Empty<System.Reflection.Assembly>();

        public void RegisterServices(IServiceCollection serviceCollection)
        {
        }

        public bool SaveManifest(PluginManifest manifest, string path) => true;

        public Task<bool> PopulateManifest(PackageInfo packageInfo, Version version, string path, PluginStatus status)
            => Task.FromResult(true);

        public void ImportPluginFrom(string folder)
        {
        }

        public void FailPlugin(System.Reflection.Assembly assembly)
        {
        }

        public void DisablePlugin(LocalPlugin plugin)
        {
        }

        public void EnablePlugin(LocalPlugin plugin)
        {
        }

        public LocalPlugin? GetPlugin(Guid id, Version? version = null)
            => _plugins.FirstOrDefault(plugin => plugin.Id == id);

        public bool RemovePlugin(LocalPlugin plugin) => true;
    }
}
