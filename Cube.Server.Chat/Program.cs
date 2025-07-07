using Cube.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cube.Server.Chat;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            // 로그 설정은 appsettings.json 에서 설정을 자동 적용
            .ConfigureServices((context, services) =>
            {
                CoreHelper.AddServices(services, context.Configuration);
                ServiceRegister.AddServices(services);

                CoreHelper.AddHostedServices(services);
                ServiceRegister.AddHostedServices(services);
            })
            .Build();

        await host.RunAsync();
    }
}
