using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Common.Network;

using Server.Chat.User;
using Server.Chat.Session;
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
            .ConfigureLogging((context, logging) =>
            {
                ServiceRegister.RegisterLogging(logging);
            })
            .ConfigureServices((context, services) =>
            {
                ServiceRegister.RegisterServices(services);
                services.AddTransient<App>();
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
        private readonly IChatSessionManager _sessionManager;

        public App(IObjectFactoryHelper objectFactory, ILoggerFactory loggerFactory)
        {
            LoggerFactoryHelper.Initialize(loggerFactory);
            _logger = LoggerFactoryHelper.CreateLogger<App>();
            _logger.LogInformation("서버 초기화...");

            _userManager = objectFactory.Create<IUserManager>();
            _sessionManager = objectFactory.Create<IChatSessionManager>();
            _packetDispatcher = objectFactory.Create<IPacketDispatcher>();

            RegisterPacketHandler(objectFactory);
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("서버 시작...");

            var acceptorLogger = LoggerFactoryHelper.CreateLogger<SocketAcceptor>();
            var acceptor = new SocketAcceptor(acceptorLogger, OnClientConnected, NetConsts.PORT, NetConsts.MAX_CONNECTION, NetConsts.LISTEN_BACKLOG);
            await acceptor.Run();

            _sessionManager.Run();

            _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Enter 키를 누르세요.");
            Console.ReadLine();

            _logger.LogInformation("서버 종료 중...");

            acceptor.Stop();
            _userManager.Stop();
            _sessionManager.Stop();
        }

        private void RegisterPacketHandler(IObjectFactoryHelper objectFactory)
        {
            _packetDispatcher.Register(MessageType.Login, objectFactory.Create<LoginHandler>());
            _packetDispatcher.Register(MessageType.Logout, objectFactory.Create<LogoutHandler>());
            _packetDispatcher.Register(MessageType.ChatMessage, objectFactory.Create<ChatMessageHandler>());
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
