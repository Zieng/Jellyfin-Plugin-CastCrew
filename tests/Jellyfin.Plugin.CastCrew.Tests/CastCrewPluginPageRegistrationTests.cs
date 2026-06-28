using System.Xml.Serialization;
using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests;

public sealed class CastCrewPluginPageRegistrationTests
{
    [Fact]
    public void GetPages_WhenWebSyncFails_DoesNotExposeCastCrewMainMenuPluginPage()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var paths = new TestApplicationPaths(root, createWebPathDirectory: false);
            var serializer = new TestXmlSerializer();
            var plugin = new CastCrewPlugin(paths, serializer);

            var pages = plugin.GetPages().ToList();

            Assert.DoesNotContain(
                pages,
                page => page.EnableInMainMenu &&
                        string.Equals(page.DisplayName, "Cast&Crew", StringComparison.Ordinal));

            Assert.Contains(
                pages,
                page => string.Equals(page.Name, "castcrew-config", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartupSyncHostedService_OnStart_SyncsCastCrewLinkWithoutConfigurationPagesRequest()
    {
        var root = CreateTemporaryRoot();

        try
        {
            var paths = new TestApplicationPaths(root, createWebPathDirectory: true);
            var serializer = new TestXmlSerializer();
            var plugin = new CastCrewPlugin(paths, serializer);
            plugin.Configuration.EnableCastCrewMainMenuEntry = true;

            var configPath = Path.Combine(paths.WebPath, "config.json");
            var indexPath = Path.Combine(paths.WebPath, "index.html");
            File.WriteAllText(configPath, "{ \"menuLinks\": [] }");
            File.WriteAllText(indexPath, "<html><head></head><body></body></html>");

            var libraryManager = CreateNoOpProxy<ILibraryManager>();
            var userManager = CreateNoOpProxy<IUserManager>();
            var mappingService = new CastCrewLibraryPersonMappingService(
                libraryManager,
                userManager,
                NullLogger<CastCrewLibraryPersonMappingService>.Instance);

            var service = new CastCrewStartupSyncHostedService(
                paths,
                libraryManager,
                mappingService,
                NullLogger<CastCrewStartupSyncHostedService>.Instance);
            await service.StartAsync(CancellationToken.None);

            var rootJson = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
            Assert.NotNull(rootJson);
            var menuLinks = rootJson!["menuLinks"] as JsonArray;
            Assert.NotNull(menuLinks);

            var castLink = menuLinks!
                .OfType<JsonObject>()
                .FirstOrDefault(link =>
                    string.Equals(link["name"]?.GetValue<string>(), "Cast&Crew", StringComparison.Ordinal) &&
                    string.Equals(link["url"]?.GetValue<string>(), "/web/#/home?tab=cast_crew", StringComparison.Ordinal));

            Assert.NotNull(castLink);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "castcrew-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static T CreateNoOpProxy<T>()
        where T : class
    {
        return DispatchProxy.Create<T, NoOpDispatchProxy>();
    }

    private sealed class TestApplicationPaths : IApplicationPaths
    {
        public TestApplicationPaths(string root, bool createWebPathDirectory)
        {
            ProgramDataPath = root;
            WebPath = Path.Combine(root, "web");
            ProgramSystemPath = root;
            DataPath = Path.Combine(root, "data");
            ImageCachePath = Path.Combine(root, "cache", "images");
            PluginsPath = Path.Combine(root, "plugins");
            PluginConfigurationsPath = Path.Combine(PluginsPath, "configurations");
            LogDirectoryPath = Path.Combine(root, "log");
            ConfigurationDirectoryPath = Path.Combine(root, "config");
            SystemConfigurationFilePath = Path.Combine(ConfigurationDirectoryPath, "system.xml");
            CachePath = Path.Combine(root, "cache");
            TempDirectory = Path.Combine(root, "temp");
            VirtualDataPath = "%AppDataPath%";
            TrickplayPath = Path.Combine(DataPath, "trickplay");
            BackupPath = Path.Combine(DataPath, "backups");

            Directory.CreateDirectory(DataPath);
            Directory.CreateDirectory(ImageCachePath);
            Directory.CreateDirectory(PluginConfigurationsPath);
            Directory.CreateDirectory(LogDirectoryPath);
            Directory.CreateDirectory(ConfigurationDirectoryPath);
            Directory.CreateDirectory(CachePath);
            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(TrickplayPath);
            Directory.CreateDirectory(BackupPath);

            if (createWebPathDirectory)
            {
                Directory.CreateDirectory(WebPath);
            }
        }

        public string ProgramDataPath { get; }

        public string WebPath { get; }

        public string ProgramSystemPath { get; }

        public string DataPath { get; }

        public string ImageCachePath { get; }

        public string PluginsPath { get; }

        public string PluginConfigurationsPath { get; }

        public string LogDirectoryPath { get; }

        public string ConfigurationDirectoryPath { get; }

        public string SystemConfigurationFilePath { get; }

        public string CachePath { get; }

        public string TempDirectory { get; }

        public string VirtualDataPath { get; }

        public string TrickplayPath { get; }

        public string BackupPath { get; }

        public void MakeSanityCheckOrThrow()
        {
        }

        public void CreateAndCheckMarker(string path, string markerName, bool recursive)
        {
        }
    }

    private class NoOpDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            var returnType = targetMethod.ReturnType;

            if (returnType == typeof(string))
            {
                return string.Empty;
            }

            if (returnType.IsValueType)
            {
                return Activator.CreateInstance(returnType);
            }

            if (returnType.IsArray)
            {
                return Array.CreateInstance(returnType.GetElementType()!, 0);
            }

            return null;
        }
    }

    private sealed class TestXmlSerializer : IXmlSerializer
    {
        public object DeserializeFromStream(Type type, Stream stream)
        {
            var serializer = new XmlSerializer(type);
            return serializer.Deserialize(stream)
                   ?? Activator.CreateInstance(type)
                   ?? throw new InvalidOperationException($"Unable to create instance of {type.FullName}.");
        }

        public void SerializeToStream(object obj, Stream stream)
        {
            var serializer = new XmlSerializer(obj.GetType());
            serializer.Serialize(stream, obj);
        }

        public void SerializeToFile(object obj, string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using var stream = File.Create(file);
            SerializeToStream(obj, stream);
        }

        public object DeserializeFromFile(Type type, string file)
        {
            if (!File.Exists(file))
            {
                return Activator.CreateInstance(type)
                       ?? throw new InvalidOperationException($"Unable to create instance of {type.FullName}.");
            }

            using var stream = File.OpenRead(file);
            return DeserializeFromStream(type, stream);
        }

        public object DeserializeFromBytes(Type type, byte[] buffer)
        {
            using var stream = new MemoryStream(buffer, writable: false);
            return DeserializeFromStream(type, stream);
        }
    }
}
