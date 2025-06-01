using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Cube.Core;
using Cube.Core.Pool;
using Cube.Core.Sessions;
using Cube.Core.Router;
using Cube.Core.Execution;
using Cube.Packet;
using Cube.Server.Chat.Session;
using Cube.Server.Chat.User;
using Cube.Server.Chat.Processor;
using Cube.Server.Chat.Service;



namespace Cube.Server.Chat;

public static class ServiceRegister
{
    public static void RegisterLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Trace);

        logging.AddConsole(options =>
        {
            options.FormatterName = "simple";
        }).AddSimpleConsole(options =>
        {
            options.TimestampFormat = "[HH:mm:ss] ";
            options.SingleLine = true;
            options.IncludeScopes = true;
        });
    }

    public static void RegisterServices(IServiceCollection services)
    {
        // 헬퍼 등록
        services.AddSingleton<IObjectFactoryHelper, ObjectFactoryHelper>();
        services.AddSingleton<IManagerContext, ManagerContext>();

        // 기본 서비스
        services.AddSingleton<SessionHeartbeat>();
        services.AddSingleton<IHeartbeat>(sp => sp.GetRequiredService<SessionHeartbeat>());
        services.AddSingleton<NetworkService>();
        services.AddSingleton<INetworkService>(sp => sp.GetRequiredService<NetworkService>());
        services.AddSingleton<IFunctionRouter, FunctionRouter>();
        services.AddSingleton<IResourceManager>(sp => new ResourceManager(sp.GetRequiredService<ILoggerFactory>(), true));
        services.AddSingleton<IPoolHandler<SocketAsyncEventArgs>>(sp => sp.GetRequiredService<IResourceManager>().GetPoolHandler(ResourceKey.SocketAsyncEventArgsPool));

        services.AddSingleton<INetworkManager, NetworkManager>();

        // 관리 매니저
        services.AddSingleton<IUserManager, UserManager>();
        services.AddSingleton<IChatSessionManager, ChatSessionManager>();
        services.AddSingleton<IPacketProcessor, PacketProcessor>();
        services.AddSingleton<IProcessor>(sp => sp.GetRequiredService<IPacketProcessor>());

        // 패킷 실행
        services.AddTransient<AuthDispatcher>();
        services.AddTransient<ChatDispatcher>();

        // 로직 서비스
        services.AddSingleton<IChatService, ChatService>();
    }

    public static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddHostedService<App>();
        services.AddHostedService(provider => provider.GetRequiredService<SessionHeartbeat>());
        services.AddHostedService(provider => provider.GetRequiredService<NetworkService>());
    }
}
