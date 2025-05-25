//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.DependencyInjection;

//namespace DummyClient;

//public class App
//{
//    private readonly ILogger<App> _logger;
//    private readonly IServiceProvider _serviceProvider;
//    private readonly List<Client> _clients = new();

//    public App(ILogger<App> logger, IServiceProvider serviceProvider)
//    {
//        _logger = logger;
//        _serviceProvider = serviceProvider;
//    }

//    public async Task RunSimulationAsync(int clientCount, int durationSec, string host = "127.0.0.1", int port = 7777)
//    {
//        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
//        var usedNames = new HashSet<string>();
//        while (usedNames.Count < clientCount)
//        {
//            var name = $"user_{Random.Shared.Next(10000, 99999)}";
//            if (usedNames.Add(name))
//            {
//                var client = new Client(loggerFactory, name);
//                _clients.Add(client);
//            }
//        }

//        _logger.LogInformation("[SIM] 시뮬레이션 시작: 클라이언트 {clientCount}명, {durationSec}초", clientCount, durationSec);
//        var tasks = new List<Task>();
//        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
//        for (var i = 0; i < _clients.Count; i++)
//        {
//            tasks.Add(_clients[i].RunAsync(host, port, cts.Token));
//            await Task.Delay(10); // 접속 타이밍 분산
//        }
//        _logger.LogInformation("[SIM] 모든 클라이언트 실행 완료");
//        await Task.WhenAll(tasks);

//        await Task.Delay(durationSec * 1000);
//        foreach (var client in _clients)
//        {
//            client.Close();
//        }

//        _logger.LogInformation("[SIM] 시뮬레이션 종료");

//    }
//}

