using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace __DummyClient;

public class App(ILogger<App> logger, IServiceProvider serviceProvider) : IDisposable
{
    private readonly ILogger<App> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private bool _disposed = false;
    private List<DummyClient> _clients = new();

    public async Task RunSimulationAsync(int clientCount, int durationSec, string host = "127.0.0.1", int port = 7777)
    {
        _logger.LogInformation("[SIM] 시뮬레이션 시작: 클라이언트 {clientCount}명, {durationSec}초", clientCount, durationSec);
        var tasks = new List<Task>();
        var rnd = new Random();
        for (int i = 0; i < clientCount; i++)
        {
            var name = $"user_{rnd.Next(10000, 99999)}";
            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var client = new DummyClient(loggerFactory, name, host, port);
            _clients.Add(client);
            tasks.Add(client.RunAsync());
            await Task.Delay(rnd.Next(10, 100)); // 접속 타이밍 분산
        }
        _logger.LogInformation("[SIM] 모든 더미 클라이언트 실행 완료");
        await Task.Delay(durationSec * 1000);
        _logger.LogInformation("[SIM] 시뮬레이션 종료 중...");
        var stopTasks = _clients.ConvertAll(c => c.StopAsync());
        await Task.WhenAll(stopTasks);
        _logger.LogInformation("[SIM] 모든 더미 클라이언트 종료 완료");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            foreach (var client in _clients)
            {
                client.StopAsync().Wait();
            }
            _clients.Clear();
        }
        _disposed = true;
    }
}

