using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;

using Server.Chat.User;
using Server.Chat.Sessions;
using Common.Network.Handler;
using Server.Chat.Handler;
using Server.Chat.Helper;


namespace Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                //config.SetBasePath(Directory.GetCurrentDirectory());
                //config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // 공통 서비스
                services.AddSingleton<SessionHeartbeat>();
                services.AddSingleton<SocketEventArgsPool>();
                services.AddSingleton<IPacketDispatcher, PacketDispatcher>();

                // 사용자 관리
                services.AddSingleton<IUserManager, UserManager>();
                services.AddSingleton<ISessionManager, SessionManager>();

                // 핸들러 등록
                services.AddTransient<LoginHandler>();
                services.AddTransient<LogoutHandler>();
                services.AddTransient<ChatMessageHandler>();

                // 비즈니스 로직 서비스 등록
                services.AddTransient<App>();

                // 헬퍼 등록
                services.AddSingleton<ILoggerFactoryHelper, LoggerFactoryHelper>();
            })
            .Build();

        var app = host.Services.GetRequiredService<App>();
        await app.RunAsync();
    }

    public class App
    {
        private readonly ILogger _logger;
        private readonly IUserManager _userManager;
        private readonly IPacketDispatcher _packetDispatcher;
        private readonly ISessionManager _sessionManager;

        public App(IServiceProvider sp)
        {
            _logger = LoggerFactoryHelper.Instance.CreateLogger<App>();

            _userManager = sp.GetRequiredService<IUserManager>();
            _sessionManager = sp.GetRequiredService<ISessionManager>();
            _packetDispatcher = sp.GetRequiredService<IPacketDispatcher>();

            RegisterPacketHandler(sp);
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("서버 시작 중...");

            var acceptorLogger = LoggerFactoryHelper.Instance.CreateLogger<SocketAcceptor>();
            var acceptor = new SocketAcceptor(acceptorLogger, OnClientConnected, NetConsts.PORT, NetConsts.MAX_CONNECTION, NetConsts.LISTEN_BACKLOG);
            await acceptor.Begin();

            _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Enter 키를 누르세요.");
            Console.ReadLine();

            _logger.LogInformation("서버 종료 중...");

            acceptor.End();
            _userManager.End();
            _sessionManager.End();
        }

        private void RegisterPacketHandler(IServiceProvider sp)
        {
            _packetDispatcher.Register(MessageType.Login, sp.GetRequiredService<LoginHandler>());
            _packetDispatcher.Register(MessageType.Logout, sp.GetRequiredService<LogoutHandler>());
            _packetDispatcher.Register(MessageType.ChatMessage, sp.GetRequiredService<ChatMessageHandler>());
        }

        private async Task OnClientConnected(Socket socket)
        {
            if (!_sessionManager.CreateSession(socket))
            {
                socket.Close();
                return;
            }

            await Task.CompletedTask;
        }
    }
}
