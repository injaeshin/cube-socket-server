using Cube.Core;
using Cube.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cube.Server.Chat;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("Appsettings.json", optional: false, reloadOnChange: true);
            })
            // 로그 설정은 appsettings.json 에서 설정을 자동 적용
            .ConfigureServices((context, services) =>
            {
                AppSettings.Initialize(context.Configuration);
                LoggerFactoryHelper.Initialize(LoggerFactory.Create(builder => builder.AddConsole()));

                CoreHelper.AddServices(services);
                ServiceRegister.AddServices(services);

                CoreHelper.AddHostedServices(services);
                ServiceRegister.AddHostedServices(services);
            })
            .Build();

        await host.RunAsync();
    }
}
