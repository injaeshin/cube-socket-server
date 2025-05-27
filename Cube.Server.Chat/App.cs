using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Server.Chat.Session;
using Cube.Server.Chat.Processor;



namespace Cube.Server.Chat;

public class App : IHostedService
{
    private readonly ILogger _logger;
    private readonly INetworkManager _networkManager;
    private readonly IChatSessionManager _sessionManager;
    private readonly IPacketProcessor _packetProcessor;
    private readonly TaskCompletionSource _shutdown = new();

    public App(IObjectFactoryHelper objectFactory, ILoggerFactory loggerFactory)
    {
        LoggerFactoryHelper.Initialize(loggerFactory);
        _logger = LoggerFactoryHelper.CreateLogger<App>();
        _logger.LogInformation("서버 초기화...");

        _networkManager = objectFactory.Create<INetworkManager>();
        _sessionManager = objectFactory.Create<IChatSessionManager>();
        _packetProcessor = objectFactory.CreateWithParameters<PacketProcessor>(loggerFactory, objectFactory, _sessionManager);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var sessionIOEvent = new SessionIOHandler { OnPacketSendAsync = _networkManager.OnSendAsync, OnPacketReceived = _packetProcessor.OnReceivedAsync, OnSessionClosed = _packetProcessor.OnSessionClosed };
        _sessionManager.Run(sessionIOEvent);

        _networkManager.BindSessionCreator(_sessionManager);
        _networkManager.Run(TransportType.Tcp, 7777);
        //_networkManager.Run(TransportType.Udp, 7778);

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
}