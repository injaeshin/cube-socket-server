using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Network;
using Cube.Packet;

namespace Cube.Core.Pool;

public interface ITcpConnectionPool : IAsyncResource
{
    ITcpConnection Rent(Socket socket);
    void Return(ITcpConnection connection);
    void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs args);
    void ReturnPacketBuffer(PacketBuffer buffer);
}

public class TcpConnectionPool : ITcpConnectionPool
{
    private readonly ObjectPool<ITcpConnection> _connPool;
    private readonly PacketBufferPool _bufferPool;
    private readonly IPoolHandler<SocketAsyncEventArgs> _saeaPoolHandler;

    private volatile bool _stopped = false;

    public TcpConnectionPool(ILoggerFactory loggerFactory, IPoolHandler<SocketAsyncEventArgs> poolHandler, int poolSize)
    {
        _saeaPoolHandler = poolHandler;
        _bufferPool = new PacketBufferPool(loggerFactory, poolSize);
        _connPool = new ObjectPool<ITcpConnection>(() => new TcpConnection(loggerFactory, this), poolSize);
    }

    public ITcpConnection Rent(Socket socket)
    {
        if (_stopped) throw new ObjectDisposedException(nameof(TcpConnectionPool));

        var tcpConnection = _connPool.Rent() ?? throw new InvalidOperationException("TCP 풀 미생성");
        var buffer = _bufferPool.Rent() ?? throw new InvalidOperationException("패킷 버퍼 미생성");
        var args = _saeaPoolHandler.Rent() ?? throw new InvalidOperationException("수신 버퍼 대여 실패");

        tcpConnection.Bind(socket, buffer, args);
        return tcpConnection;
    }

    public void Return(ITcpConnection tcpConnection) => _connPool.Return(tcpConnection);
    public void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs args) => _saeaPoolHandler.Return(args);
    public void ReturnPacketBuffer(PacketBuffer buffer) => _bufferPool.Return(buffer);


    public string Name { get; init; } = "TcpConnectionPool";
    public int Count => _connPool.Count;
    public int InUse => _connPool.InUse;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) throw new InvalidOperationException("TransportPool is already closed");
        _stopped = true;

        await _bufferPool.StopAsync(timeout);
        await _connPool.StopAsync(timeout);
    }
}