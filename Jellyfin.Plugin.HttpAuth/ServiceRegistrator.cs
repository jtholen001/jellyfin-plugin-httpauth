using MediaBrowser.Controller;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HttpAuth
{
    // The purpose of this class is to register the HttpAuthProvider as a custom Authentification provider,
    // and the service that injects our script into index.html through the File Transformation plugin.
    public class ServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<IAuthenticationProvider, HttpAuthProvider>();
            serviceCollection.AddHostedService<FileTransformationRegistrar>();
        }
    }
}
