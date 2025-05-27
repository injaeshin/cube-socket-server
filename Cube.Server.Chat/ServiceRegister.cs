using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Packet;

using Cube.Server.Chat.Session;
using Cube.Server.Chat.Handler;
using Cube.Server.Chat.Service;
using Cube.Server.Chat.User;
using Cube.Server.Chat.Helper;

namespace Cube.Server.Chat;

public static class ServiceRegister
{
    public static void RegisterLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);

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

        // 공통 서비스
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<SessionHeartbeat>();

        // 관리
        services.AddSingleton<IUserManager, UserManager>();
        services.AddSingleton<INetworkManager, NetworkManager>();
        services.AddSingleton<IChatSessionManager, ChatSessionManager>();
        services.AddSingleton<IPacketDispatcher, PacketDispatcher>();

        // 핸들러 등록
        services.AddTransient<LoginHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<ChatMessageHandler>();
    }

    public static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddHostedService(provider => provider.GetRequiredService<SessionHeartbeat>());
        services.AddHostedService<App>();
    }
}
