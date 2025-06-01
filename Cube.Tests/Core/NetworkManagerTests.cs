//using System.Net.Sockets;
//using Microsoft.Extensions.Logging;
//using Xunit.Abstractions;
//using Moq;
//using Cube.Core;
//using Cube.Core.Sessions;
//using Cube.Core.Network;
//using Cube.Core.Pool;

//namespace Cube.Tests.Core;

//public class NetworkManagerTest
//{
//    private readonly ILoggerFactory _loggerFactory;
//    private readonly ITestOutputHelper _outputHelper;

//    private Mock<ISessionManagerCreator> _sessionCreatorMock;

//    public NetworkManagerTest(ITestOutputHelper outputHelper)
//    {
//        _outputHelper = outputHelper;
//        _loggerFactory = LoggerFactory.Create(builder =>
//        {
//            builder
//                .SetMinimumLevel(LogLevel.Trace)
//                .AddConsole()
//                .AddProvider(new XunitLoggerProvider(_outputHelper));
//        });

//        _sessionCreatorMock = new Mock<ISessionManagerCreator>();
//        _sessionCreatorMock.Setup(x => x.CreateAndRun(It.IsAny<ITcpConnection>())).Returns(true);
//    }

//    [Fact]
//    public void StartAndStop()
//    {
//        //var networkManager = new NetworkManager(_loggerFactory, networkService);
//        //networkManager.Init(_sessionCreatorMock.Object, shouldUdp: true);
//        //networkManager.Run(1234, 1235);

//        //networkManager.Close();

//        Assert.True(true);
//    }
//}
