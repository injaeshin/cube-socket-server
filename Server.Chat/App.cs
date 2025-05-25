using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Cube.Network;
using Cube.Server.Chat.Helper;
using Cube.Server.Chat.Session;
using Cube.Session;

namespace Cube.Server.Chat;

public class App : IHostedService
{
    private readonly ILogger _logger;
    // private readonly IUserManager _userManager;
    // private readonly IPacketDispatcher _packetDispatcher;

    private readonly INetworkManager _networkManager;
    private readonly IChatSessionManager _sessionManager;

    private readonly TaskCompletionSource _shutdown = new();

    public App(IObjectFactoryHelper objectFactory, ILoggerFactory loggerFactory)
    {
        LoggerFactoryHelper.Initialize(loggerFactory);
        _logger = LoggerFactoryHelper.CreateLogger<App>();
        _logger.LogInformation("서버 초기화...");

        // NetworkManager, SessionManager, PacketDispatcher 생성
        _networkManager = objectFactory.Create<INetworkManager>();

        //_userManager = objectFactory.Create<IUserManager>();
        _sessionManager = objectFactory.Create<IChatSessionManager>();
        //_packetDispatcher = objectFactory.Create<IPacketDispatcher>();

        RegisterPacketHandler(objectFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var sessionIOEvent = new SessionIOEvent { OnSendEnqueueAsync = _networkManager.OnSendEnqueueAsync, OnReceived = null!, };
        _sessionManager.Run(sessionIOEvent);

        _networkManager.BindSessionCreator(_sessionManager);
        _networkManager.Run(TransportType.Tcp, 7777);
        _networkManager.Run(TransportType.Udp, 7778);


        _logger.LogInformation("서버 시작...");
        _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Ctrl + C 키를 누르세요.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("서버 종료 중...");
        _networkManager.Stop();

        _logger.LogInformation("서버가 종료되었습니다.");

        _shutdown.TrySetResult();
        return Task.CompletedTask;
    }

    private void RegisterPacketHandler(IObjectFactoryHelper objectFactory)
    {
        //_packetDispatcher.Register(MessageType.Login, objectFactory.Create<LoginHandler>());
        //_packetDispatcher.Register(MessageType.Logout, objectFactory.Create<LogoutHandler>());
        //_packetDispatcher.Register(MessageType.ChatMessage, objectFactory.Create<ChatMessageHandler>());
    }
}