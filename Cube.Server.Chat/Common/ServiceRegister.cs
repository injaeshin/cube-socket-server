using Microsoft.Extensions.DependencyInjection;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Core.Execution;
using Cube.Server.Chat.Session;
using Cube.Server.Chat.User;
using Cube.Server.Chat.Processor;
using Cube.Server.Chat.Service;

namespace Cube.Server.Chat;

public static class ServiceRegister
{
    public static void AddServices(IServiceCollection services)
    {
        // 헬퍼 등록
        services.AddSingleton<IObjectFactoryHelper, ObjectFactoryHelper>();
        services.AddSingleton<IManagerContext, ManagerContext>();

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

    public static void AddHostedServices(IServiceCollection services)
    {
        services.AddHostedService<App>();
        services.AddHostedService(provider => provider.GetRequiredService<SessionHeartbeat>());
        services.AddHostedService(provider => provider.GetRequiredService<NetworkService>());
    }
}
