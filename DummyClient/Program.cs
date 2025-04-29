using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using __DummyClient;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Trace);
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
        services.AddTransient<App>();
        services.AddTransient(typeof(ILogger<DummyClient>), sp =>
        {
            var factory = sp.GetRequiredService<ILoggerFactory>();
            return factory.CreateLogger<DummyClient>();
        });
    })
    .Build();

// 파라미터 입력 (기본값: 10명, 30초)
int clientCount = 10;
int durationSec = 30;

Console.WriteLine($"동시 접속 클라이언트 수 입력 (기본: {clientCount}): ");
var input = Console.ReadLine();
if (int.TryParse(input, out var cc) && cc > 0) clientCount = cc;
Console.WriteLine($"시뮬레이션 시간(초) 입력 (기본: {durationSec}): ");
input = Console.ReadLine();
if (int.TryParse(input, out var ds) && ds > 0) durationSec = ds;

var app = host.Services.GetRequiredService<App>();
await app.RunSimulationAsync(clientCount, durationSec);

Console.WriteLine("시뮬레이션 종료. 아무 키나 누르세요...");
Console.ReadKey();

