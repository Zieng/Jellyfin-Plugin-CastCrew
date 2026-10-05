using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CastCrew;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CastCrewActorQueryService>();
        serviceCollection.AddSingleton<Metadata.MetadataSearchService>();
        serviceCollection.AddSingleton<Metadata.IMetadataProvider, Metadata.TmdbMetadataProvider>();
        serviceCollection.AddSingleton<Metadata.IMetadataProvider, Metadata.MasexMetadataProvider>();
        serviceCollection.AddSingleton<Metadata.IMetadataProvider, Metadata.NetflavMetadataProvider>();
        serviceCollection.AddSingleton<CastCrewLibraryPersonMappingService>();
        serviceCollection.AddHostedService<CastCrewStartupSyncHostedService>();
        serviceCollection.AddTransient<IStartupFilter, CastCrewStartupFilter>();
    }
}
