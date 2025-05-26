using Microsoft.Extensions.Hosting;

using Cube.Server.Chat.Helper;

namespace Cube.Server.Chat;

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
                ServiceRegister.RegisterHostedServices(services);
            })
            .Build();

        await host.RunAsync();
    }
}
