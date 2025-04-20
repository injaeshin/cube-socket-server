using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using __DummyClient;

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
        services.AddTransient<App>();
    })
    .Build();

int clientCount = 1;
var clients = new List<Task>();

for (int i = 0; i < clientCount; i++)
{
    var client = host.Services.GetRequiredService<App>();
    clients.Add(client.RunAsync());
}

await Task.WhenAll(clients);

// This line may be redundant if you're already running multiple clients above
// var app = host.Services.GetRequiredService<App>();
// await app.RunAsync();

