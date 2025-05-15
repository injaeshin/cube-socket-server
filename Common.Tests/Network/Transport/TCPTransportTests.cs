using Moq;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using Common.Network.Transport;
using Common.Network;
using Common.Network.Packet;
using Common.Network.Pool;

namespace Common.Tests.Network.Transport;

public class TcpTransportTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly TcpTransportPool _tcpTransportPool;
    private const int TestPort = 12345;

    public TcpTransportTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _tcpTransportPool = new TcpTransportPool(_loggerFactory);
    }

    private (Socket serverSocket, Socket clientSocket)
    CreateConnectedSockets(int port = TestPort)
    {
        var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        serverSocket.Listen(1);

        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var connectTask = Task.Run(() => clientSocket.Connect(IPAddress.Loopback, port));
        var acceptTask = Task.Run(() => serverSocket.Accept());

        Task.WaitAll(connectTask, acceptTask);

        var acceptedSocket = acceptTask.Result;
        return (acceptedSocket, clientSocket);
    }

    private void CleanupTestEnvironment(Socket serverSocket, Socket clientSocket, ITransport transport)
    {
        clientSocket?.Close();
        serverSocket?.Close();
    }

    //private byte[] ReceiveAll(Socket socket, int expectedLength)
    //{
    //    var receivedData = new byte[expectedLength];
    //    int totalReceived = 0;

    //    while (totalReceived < expectedLength)
    //    {
    //        int remaining = expectedLength - totalReceived;
    //        int received = socket.Receive(receivedData, totalReceived, remaining, SocketFlags.None);

    //        if (received == 0) // 연결이 종료된 경우
    //        {
    //            throw new SocketException((int)SocketError.ConnectionReset);
    //        }

    //        totalReceived += received;
    //    }

    //    return receivedData;
    //}

    //// 또는 비동기 버전:
    //private async Task<byte[]> ReceiveAllAsync(Socket socket, int expectedLength)
    //{
    //    var receivedData = new byte[expectedLength];
    //    int totalReceived = 0;

    //    while (totalReceived < expectedLength)
    //    {
    //        int remaining = expectedLength - totalReceived;
    //        int received = await Task.Run(() =>
    //            socket.Receive(receivedData, totalReceived, remaining, SocketFlags.None));

    //        if (received == 0)
    //        {
    //            throw new SocketException((int)SocketError.ConnectionReset);
    //        }

    //        totalReceived += received;
    //    }

    //    return receivedData;
    //}

    //[Fact]
    //public void IsConnectionAlive_WithConnectedSocket_ReturnsTrue()
    //{
    //    // Arrange
    //    var (serverSocket, clientSocket) = CreateConnectedSockets();
    //    var mockNotify = new Mock<ITransportNotify>();
    //    var transport = _tcpTransportPool.Rent(clientSocket);
    //    try
    //    {
    //        transport.BindNotify(mockNotify.Object);

    //        // Act
    //        bool result = transport.IsConnectionAlive();

    //        // Assert
    //        Assert.True(result);
    //    }
    //    finally
    //    {
    //        CleanupTestEnvironment(serverSocket, clientSocket, transport);
    //    }
    //}

    //[Fact]
    //public async Task SendAsync_WhenSocketConnected_DataIsReceived()
    //{
    //    // Arrange
    //    var (serverSocket, clientSocket) = CreateConnectedSockets();
    //    var mockNotify = new Mock<ITransportNotify>();
    //    var transport = _tcpTransportPool.Rent(clientSocket);

    //    try
    //    {
    //        transport.BindNotify(mockNotify.Object);

    //        // Act
    //        await transport.SendAsync(testData);

    //        // 서버 측에서 데이터 수신
    //        byte[] receivedData = await ReceiveAllAsync(serverSocket, testData.Length);

    //        // Assert
    //        Assert.Equal(testData, receivedData);
    //    }
    //    finally
    //    {
    //        CleanupTestEnvironment(serverSocket, clientSocket, transport, receiveArgs);
    //    }
    //}

    //[Fact]
    //public async Task SendAsync_WhenSendingLargeData_AllDataIsReceived()
    //{
    //    // Arrange
    //    var (serverSocket, clientSocket, transport, receiveArgs) = CreateConnectedSockets();
    //    var mockNotify = new Mock<ITransportNotify>();

    //    try
    //    {
    //        // 큰 크기의 테스트 데이터 생성 (예: 100KB)
    //        byte[] testData = new byte[102400];
    //        new Random().NextBytes(testData);  // 랜덤 데이터로 채우기

    //        transport.BindSocket(clientSocket, receiveArgs);
    //        transport.BindNotify(mockNotify.Object);

    //        // Act
    //        await transport.SendAsync(testData);

    //        // 서버 측에서 데이터 수신
    //        byte[] receivedData = await ReceiveAllAsync(serverSocket, testData.Length);

    //        // Assert
    //        Assert.Equal(testData, receivedData);
    //    }
    //    finally
    //    {
    //        CleanupTestEnvironment(serverSocket, clientSocket, transport, receiveArgs);
    //    }
    //}

    //[Fact]
    //public async Task SendAsync_WhenSendingMultiplePackets_AllDataIsReceivedInOrder()
    //{
    //    // Arrange
    //    var (serverSocket, clientSocket, transport, receiveArgs) = CreateConnectedSockets();
    //    var mockNotify = new Mock<ITransportNotify>();

    //    using var packet = new PacketWriter().WriteType(MessageType.Ping);

    //    try
    //    {
    //        // transport.BindSocket(clientSocket, receiveArgs);
    //        // transport.BindNotify(mockNotify.Object);

    //        // // Act
    //        // foreach (var packet in testPackets)
    //        // {
    //        //     await transport.SendAsync(packet);

    //        //     // 각 패킷 수신
    //        //     byte[] receivedData = await ReceiveAllAsync(serverSocket, packet.Length);

    //        //     // Assert - 각 패킷이 순서대로 도착했는지 확인
    //        //     Assert.Equal(packet, receivedData);
    //        // }
    //    }
    //    finally
    //    {
    //        CleanupTestEnvironment(serverSocket, clientSocket, transport, receiveArgs);
    //    }
    //}

    //[Fact]
    //public void Close_WhenCalled_SocketDisconnectsGracefully()
    //{
    //    // Arrange
    //    var (serverSocket, clientSocket, transport, receiveArgs) = CreateConnectedSockets();
    //    var mockNotify = new Mock<ITransportNotify>();

    //    try
    //    {
    //        transport.BindSocket(clientSocket, receiveArgs);
    //        transport.BindNotify(mockNotify.Object);

    //        // Act
    //        transport.Close(true);

    //        // Assert
    //        Assert.False(clientSocket.Connected);
    //        mockNotify.Verify(n => n.OnNotifyDisconnected(true), Times.Once);
    //    }
    //    finally
    //    {
    //        CleanupTestEnvironment(serverSocket, clientSocket, transport, receiveArgs);
    //    }
    //}

    [Fact]
    public async Task ReceiveData_WhenDataSentFromServer_NotifiesListener()
    {
        // Arrange
        var (serverSocket, clientSocket) = CreateConnectedSockets();
        var mockNotify = new Mock<ITransportNotify>();
        var transport = _tcpTransportPool.Rent(clientSocket);

        using var packet = new PacketWriter().WriteType(MessageType.Ping);
        packet.WriteInt(TestPort);

        try
        {
            var receivedType = MessageType.None;
            var receivedData = 0;

            mockNotify
                .Setup(n => n.OnNotifyReceived(It.IsAny<MessageType>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<byte[]>()))
                .Callback<MessageType, ReadOnlyMemory<byte>, byte[]>((type, data, buffer) =>
                {
                    receivedType = type;
                    receivedData = (data.Span[0] << 24) | (data.Span[1] << 16) | (data.Span[2] << 8) | data.Span[3];
                })
                .Returns(Task.CompletedTask);

            transport.BindNotify(mockNotify.Object);
            transport.Run();

            // Act
            await serverSocket.SendAsync(packet.ToPacket());

            // 데이터 수신을 위한 짧은 대기
            await Task.Delay(100);

            // Assert
            Assert.Equal(MessageType.Ping, receivedType);
            Assert.Equal(TestPort, receivedData);
        }
        finally
        {
            CleanupTestEnvironment(serverSocket, clientSocket, transport);
        }
    }

    [Fact]
    public async Task Transport_WhenRentAndReturn_CanBeReused()
    {
        var mockNotify = new Mock<ITransportNotify>();
        mockNotify
            .Setup(n => n.OnNotifyReceived(It.IsAny<MessageType>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        // First Use
        var (serverSocket1, clientSocket1) = CreateConnectedSockets();

        var transport1 = _tcpTransportPool.Rent(clientSocket1);
        var transport1Hash = transport1.GetHashCode();

        try
        {
            using var packet1 = new PacketWriter().WriteType(MessageType.Ping);
            packet1.WriteInt(TestPort);

            transport1.BindNotify(mockNotify.Object);
            transport1.Run();

            await serverSocket1.SendAsync(packet1.ToPacket());
            await Task.Delay(100);
        }
        finally
        {
            CleanupTestEnvironment(serverSocket1, clientSocket1, transport1);
        }

        await Task.Delay(100);

        // Second Use
        var (serverSocket2, clientSocket2) = CreateConnectedSockets(TestPort + 1);
        var transport2 = _tcpTransportPool.Rent(clientSocket2);
        var transport2Hash = transport2.GetHashCode();

        try
        {
            using var packet2 = new PacketWriter().WriteType(MessageType.Ping);
            packet2.WriteInt(TestPort);

            transport2.BindNotify(mockNotify.Object);
            transport2.Run();

            await serverSocket2.SendAsync(packet2.ToPacket());
            await Task.Delay(100);

            // Assert
            Assert.Equal(transport1Hash, transport2Hash);
        }
        finally
        {
            CleanupTestEnvironment(serverSocket2, clientSocket2, transport2);
        }

        // Third Use
        var (serverSocket3, clientSocket3) = CreateConnectedSockets(TestPort + 2);
        var mockNotify3 = new Mock<ITransportNotify>();
        var transport3 = _tcpTransportPool.Rent(clientSocket3);
        var transport3Hash = transport3.GetHashCode();

        try
        {
            using var packet3 = new PacketWriter().WriteType(MessageType.Ping);
            packet3.WriteInt(TestPort);

            transport3.BindNotify(mockNotify3.Object);
            transport3.Run();

            await serverSocket3.SendAsync(packet3.ToPacket());
            await Task.Delay(100);

            // Assert
            Assert.Equal(transport2Hash, transport3Hash);
        }
        finally
        {
            CleanupTestEnvironment(serverSocket3, clientSocket3, transport3);
        }
    }

    public void Dispose()
    {
        _tcpTransportPool.Close();
    }
}