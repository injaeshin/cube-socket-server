using Microsoft.Extensions.DependencyInjection;

using Server.Chat.Handler;
using Server.Chat.Service;
using Server.Chat.Session;
using Server.Chat.User;

using Common.Network.Handler;
using Common.Network.Pool;
using Common.Network;
using Microsoft.Extensions.Logging;

namespace Server.Chat.Helper;

public static class ServiceRegister
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 공통 서비스
        services.AddSingleton(provider => new SocketAsyncEventArgsPool(provider.GetService<ILoggerFactory>()!, NetConsts.MAX_CONNECTION));
        services.AddSingleton(provider => new TcpTransportPool(provider.GetService<ILoggerFactory>()!));
        services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
        services.AddSingleton<IChatService, ChatService>();

        // 사용자 관리
        services.AddSingleton<IUserManager, UserManager>();
        services.AddSingleton<IChatSessionManager, ChatSessionManager>();

        // 핸들러 등록
        services.AddTransient<LoginHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<ChatMessageHandler>();

        // 헬퍼 등록
        services.AddSingleton<IObjectFactoryHelper, ObjectFactoryHelper>();
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
