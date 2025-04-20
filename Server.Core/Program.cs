//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Server.Login;
//using Server.Core;
//using Server.Core.Pool;

//namespace Server;

//public class Program
//{
//    public static async Task Main(string[] args)
//    {
//        var host = Host.CreateDefaultBuilder(args)
//            .ConfigureAppConfiguration((context, config) =>
//            {
//                //config.SetBasePath(Directory.GetCurrentDirectory());
//                //config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
//            })
//            .ConfigureLogging((context, logging) =>
//            {
//                logging.AddConsole();
//            })
//            .ConfigureServices((context, services) =>
//            {
//                services.AddSingleton<ISocketEventArgsPool, SocketEventArgsPool>();
//                services.AddSingleton<ILoginSessionManager, LoginSessionManager>();

//                services.AddTransient<App>();
//            })
//            .Build();

//        var app = host.Services.GetRequiredService<App>();
//        await app.RunAsync();
//    }

//    public class App(ILogger<App> logger, ILoginSessionManager loginSessionManager, ILoggerFactory loggerFactory)
//    {
//        private readonly ILogger _logger = logger;
//        private readonly ILoginSessionManager _loginSessionManager = loginSessionManager;
//        private readonly ILoggerFactory _loggerFactory = loggerFactory;

//        public async Task RunAsync()
//        {
//            _logger.LogInformation("서버 시작 중...");

//            var acceptorLogger = _loggerFactory.CreateLogger<SocketAcceptor>();
//            var acceptor = new SocketAcceptor(_loginSessionManager, acceptorLogger);
//            await acceptor.Start();

//            _logger.LogInformation("서버가 실행 중입니다. 종료하려면 Enter 키를 누르세요.");
//            Console.ReadLine();
//        }
//    }
//}
