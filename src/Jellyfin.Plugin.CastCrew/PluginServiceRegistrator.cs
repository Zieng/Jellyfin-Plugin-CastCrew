using Jellyfin.Plugin.CastCrew.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CastCrew;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CastCrewActorQueryService>();
        serviceCollection.AddHostedService<CastCrewStartupSyncHostedService>();
    }
}
