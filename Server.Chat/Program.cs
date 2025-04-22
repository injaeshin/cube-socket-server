using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Pool;
using Common.Network.Session;

using Server.Chat.Users;
using Server.Chat.Sessions;
using Common.Network.Handler;
using Common.Network.Packet;
using Server.Chat.Handler;


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
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                // 로그 출력 형식 설정 
                logging.AddConsole(options =>
                {
                    options.FormatterName = "simple";
                }).AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                    options.SingleLine = true;
                    options.IncludeScopes = true;
                });
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

                // 비즈니스 로직 서비스 등록
                services.AddTransient<App>();
            })
            .Build();

        var app = host.Services.GetRequiredService<App>();
        await app.RunAsync();
    }

    public class App(IUserManager userManager, ISessionManager sessionManager, IPacketDispatcher packetDispatcher, ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<App>();
        private readonly IUserManager _userManager = userManager;
        private readonly IPacketDispatcher _packetDispatcher = packetDispatcher;
        private readonly ISessionManager _sessionManager = sessionManager;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;

        public async Task RunAsync()
        {
            _logger.LogInformation("서버 시작 중...");

            RegisterPacketHandler();

            var acceptorLogger = _loggerFactory.CreateLogger<SocketAcceptor>();
            var acceptor = new SocketAcceptor(acceptorLogger, OnClientConnected);
            await acceptor.Begin();

            _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Enter 키를 누르세요.");
            Console.ReadLine();

            _logger.LogInformation("서버 종료 중...");

            _userManager.End();
            _sessionManager.End();
        }

        private void RegisterPacketHandler()
        {
            _packetDispatcher.Register(PacketType.Login, new LoginHandler(_loggerFactory.CreateLogger<LoginHandler>(), _userManager));
            _packetDispatcher.Register(PacketType.Logout, new LogoutHandler(_loggerFactory.CreateLogger<LogoutHandler>(), _userManager));
        }

        private async Task OnClientConnected(Socket socket)
        {
            if (_sessionManager.IsMaxConnection())
            {
                _logger.LogError("최대 접속 수 초과");
                socket.Close();
                return;
            }

            if (_sessionManager.TryRent(out var session))
            {
                session!.Run(socket);
            }
            else
            {
                _logger.LogWarning("세션 생성 실패");
                socket.Close();
            }

            await Task.CompletedTask;
        }
    }
}
