using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Core.Router;
using Cube.Core.Sessions;
using Cube.Core.Settings;


namespace Cube.Core;

public static class CoreHelper
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // 기본 서비스
        services.AddSingleton<IFunctionRouter, FunctionRouter>();

        services.AddSingleton<SessionHeartbeat>();
        services.AddSingleton<IHeartbeat>(sp => sp.GetRequiredService<SessionHeartbeat>());

        services.AddSingleton<NetworkService>();
        services.AddSingleton<INetworkService>(sp => sp.GetRequiredService<NetworkService>());
        services.AddSingleton<INetworkManager, NetworkManager>();

        services.AddSingleton<IResourceManager>(sp => new ResourceManager(sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IPoolHandler<SocketAsyncEventArgs>>(sp => sp.GetRequiredService<IResourceManager>().GetPoolHandler(ResourceKey.SocketAsyncEventArgsPool));

        return services;
    }

    public static IServiceCollection AddHostedServices(this IServiceCollection services)
    {
        services.AddHostedService(provider => provider.GetRequiredService<NetworkService>());
        services.AddHostedService(provider => provider.GetRequiredService<SessionHeartbeat>());

        return services;
    }
}
