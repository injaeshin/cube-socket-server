using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Cube.Network;
using Cube.Network.Pool;
using Cube.Session;

using Cube.Server.Chat.Session;


namespace Cube.Server.Chat.Helper;

public static class ServiceRegister
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 공통 서비스
        services.AddSingleton(provider => new SocketAsyncEventArgsPool(provider.GetService<ILoggerFactory>()!, NetConsts.MAX_CONNECTIONS));
        //services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
        //services.AddSingleton<IChatService, ChatService>();

        // 관리
        //services.AddSingleton<IUserManager, UserManager>();
        services.AddSingleton<INetworkManager, NetworkManager>();
        services.AddSingleton<IChatSessionManager, ChatSessionManager>();

        // 핸들러 등록
        //services.AddTransient<LoginHandler>();
        //services.AddTransient<LogoutHandler>();
        //services.AddTransient<ChatMessageHandler>();

        // 헬퍼 등록
        services.AddSingleton<IObjectFactoryHelper, ObjectFactoryHelper>();
    }

    public static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddSingleton<SessionHeartbeat>();
        services.AddHostedService<SessionHeartbeat>();

        services.AddSingleton<App>();
        services.AddHostedService<App>();
    }

    public static void RegisterLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Information);

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
}
