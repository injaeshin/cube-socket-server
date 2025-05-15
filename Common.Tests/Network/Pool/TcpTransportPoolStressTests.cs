using System.Net;
using System.Net.Sockets;
using Common.Network.Pool;
using Common.Network.Transport;
using Microsoft.Extensions.Logging;
using Moq;
using Common.Network;

namespace Common.Tests.Network.Pool;

public class TcpTransportPoolStressTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly TcpTransportPool _tcpTransportPool;
    private const int TestPort = 12345;
    private const int MaxConnections = 1000;
    private const int ConcurrentConnections = 100;

    public TcpTransportPoolStressTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _tcpTransportPool = new TcpTransportPool(_loggerFactory);
    }

    private (Socket serverSocket, Socket clientSocket) CreateConnectedSockets(int port)
    {
        var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        serverSocket.Listen(1);

        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectTask = Task.Run(() => clientSocket.Connect(IPAddress.Loopback, port));
        var acceptTask = Task.Run(() => serverSocket.Accept());

        Task.WaitAll(connectTask, acceptTask);
        return (acceptTask.Result, clientSocket);
    }

    private void CleanupTestEnvironment(Socket serverSocket, Socket clientSocket, ITransport transport)
    {
        clientSocket?.Close();
        serverSocket?.Close();
    }

    [Fact]
    public async Task BasicRentReturn_ShouldMaintainPoolSize()
    {
        var mockNotify = new Mock<ITransportNotify>();
        mockNotify.Setup(n => n.OnNotifyReceived(It.IsAny<MessageType>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        // Arrange
        var initialCount = _tcpTransportPool.GetCount();
        var connections = new List<(Socket server, Socket client, ITransport transport)>();

        try
        {
            // Act
            for (int i = 0; i < 10; i++)
            {
                var (server, client) = CreateConnectedSockets(TestPort + i);
                var transport = _tcpTransportPool.Rent(client);
                transport.BindNotify(mockNotify.Object);
                transport.Run();
                connections.Add((server, client, transport));
            }

            // Assert
            var afterRentCount = _tcpTransportPool.GetCount();
            Assert.Equal(initialCount.Item1 - 10, afterRentCount.Item1);
            Assert.Equal(initialCount.Item2 - 10, afterRentCount.Item2);

            // Cleanup
            foreach (var (server, client, transport) in connections)
            {
                CleanupTestEnvironment(server, client, transport);
            }

            await Task.Delay(1000); // Simulate some delay for cleanup

            // Verify final count
            var finalCount = _tcpTransportPool.GetCount();
            Assert.Equal(initialCount.Item1, finalCount.Item1);
            Assert.Equal(initialCount.Item2, finalCount.Item2);
        }
        catch
        {
            // Cleanup in case of failure
            foreach (var (server, client, transport) in connections)
            {
                CleanupTestEnvironment(server, client, transport);
            }
            throw;
        }

        await Task.CompletedTask; // Placeholder for async method
    }

    [Fact]
    public async Task ConcurrentRentReturn_ShouldHandleMultipleConnections()
    {
        // Arrange
        var initialCount = _tcpTransportPool.GetCount();
        var tasks = new List<Task>();
        var mockNotify = new Mock<ITransportNotify>();
        mockNotify.Setup(n => n.OnNotifyReceived(It.IsAny<MessageType>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        // Act
        for (int i = 0; i < ConcurrentConnections; i++)
        {
            var port = TestPort + i;
            tasks.Add(Task.Run(async () =>
            {
                var (server, client) = CreateConnectedSockets(port);
                var transport = _tcpTransportPool.Rent(client);
                try
                {
                    transport.BindNotify(mockNotify.Object);
                    transport.Run();
                    await Task.Delay(10); // Simulate some work
                }
                finally
                {
                    CleanupTestEnvironment(server, client, transport);
                }
            }));
        }

        await Task.WhenAll(tasks);

        await Task.Delay(1000); // Allow some time for cleanup

        // Assert
        var finalCount = _tcpTransportPool.GetCount();
        Assert.Equal(initialCount.Item1, finalCount.Item1);
        Assert.Equal(initialCount.Item2, finalCount.Item2);
    }

    [Fact]
    public async Task StressTest_ShouldHandleRapidRentReturn()
    {
        // Arrange
        var initialCount = _tcpTransportPool.GetCount();
        var mockNotify = new Mock<ITransportNotify>();
        mockNotify.Setup(n => n.OnNotifyReceived(It.IsAny<MessageType>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        // Act
        for (int i = 0; i < MaxConnections; i++)
        {
            var (server, client) = CreateConnectedSockets(TestPort + (i % 1000));
            var transport = _tcpTransportPool.Rent(client);
            try
            {
                transport.BindNotify(mockNotify.Object);
                transport.Run();
                await Task.Delay(1); // Minimal delay to simulate work
            }
            finally
            {
                CleanupTestEnvironment(server, client, transport);
            }

            //// Periodically check pool size
            //if (i % 100 == 0)
            //{
            //    var currentCount = _tcpTransportPool.GetCount();
            //    Assert.Equal(initialCount.Item1, currentCount.Item1);
            //    Assert.Equal(initialCount.Item2, currentCount.Item2);
            //}
        }

        await Task.Delay(1000); // Allow some time for cleanup

        // Assert
        var finalCount = _tcpTransportPool.GetCount();
        Assert.Equal(initialCount.Item1, finalCount.Item1);
        Assert.Equal(initialCount.Item2, finalCount.Item2);
    }

    public void Dispose()
    {
        _tcpTransportPool.Close();
    }
}