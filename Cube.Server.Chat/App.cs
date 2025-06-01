using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Server.Chat.Session;
using Cube.Packet;

namespace Cube.Server.Chat;

public class App : IHostedService
{
    private readonly ILogger _logger;
    private readonly INetworkManager _networkManager;
    private readonly IChatSessionManager _sessionManager;

    private readonly TaskCompletionSource _shutdown = new();

    public App(IObjectFactoryHelper objectFactory, ILoggerFactory loggerFactory)
    {
        LoggerFactoryHelper.Initialize(loggerFactory);
        _logger = LoggerFactoryHelper.CreateLogger<App>();

        _logger.LogInformation("서버 초기화...");

        _sessionManager = objectFactory.Create<IChatSessionManager>();
        _networkManager = objectFactory.Create<INetworkManager>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("서버 시작...");

        _sessionManager.Run();
        _networkManager.Run(7777, 7778);

        _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Ctrl + C 키를 누르세요.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("서버 종료 중...");

        _sessionManager.Close();
        _networkManager.Close();

        _logger.LogInformation("서버가 종료되었습니다.");

        var cnt = BufferArrayPool.GetInUseCount();
        _logger.LogWarning("버퍼 풀에 {Count}개의 사용 중인 버퍼가 남아 있습니다.", cnt);

        _shutdown.TrySetResult();
        return Task.CompletedTask;
    }
}