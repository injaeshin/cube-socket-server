using Microsoft.Extensions.DependencyInjection;

using Common.Network.Handler;
using Common.Network.Session;
using Server.Chat.Handler;
using Server.Chat.Service;
using Server.Chat.Session;
using Server.Chat.User;

namespace Server.Chat.Helper;

public static class ServiceRegister
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 공통 서비스
        services.AddSingleton<SessionHeartbeat>();
        services.AddSingleton<SocketEventArgsPool>();
        services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
        services.AddSingleton<IChatService, ChatService>();

        // 사용자 관리
        services.AddSingleton<IUserManager, UserManager>();
        services.AddSingleton<ISessionManager, SessionManager>();

        // 핸들러 등록
        services.AddTransient<LoginHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<ChatMessageHandler>();

        // 헬퍼 등록
        services.AddSingleton<ILoggerFactoryHelper, LoggerFactoryHelper>();
        services.AddSingleton<IObjectFactoryHelper, ObjectFactoryHelper>();
    }
}
