using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Network;
using Cube.Packet;
using Cube.Core.Settings;

namespace Cube.Core.Pool;

public interface IUdpConnectionPool : IAsyncResource
{
    IUdpConnection Rent(EndPoint remoteEndPoint);
    void Return(IUdpConnection endpoint);
}

public class UdpConnectionPool : IUdpConnectionPool
{
    private readonly ObjectPool<IUdpConnection> _connPool;
    private volatile bool _stopped = false;

    public UdpConnectionPool(ILoggerFactory loggerFactory, NetworkConfig networkConfig)
    {
        _connPool = new ObjectPool<IUdpConnection>(() => new UdpConnection(loggerFactory, this, networkConfig.UdpResendIntervalMs), networkConfig.MaxConnections);
    }

    public Socket Rent() => throw new NotSupportedException();
    public void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs args) => throw new NotSupportedException();
    public void ReturnPacketBuffer(PacketBuffer buffer) => throw new NotSupportedException();

    public IUdpConnection Rent(EndPoint remoteEndPoint)
    {
        if (_stopped) throw new ObjectDisposedException(nameof(UdpConnectionPool));

        var endpoint = _connPool.Rent() ?? throw new InvalidOperationException("UDP 풀 미생성");

        endpoint.BindEndPoint(remoteEndPoint);
        return endpoint;
    }

    public void Return(IUdpConnection endpoint) => _connPool.Return(endpoint);

    public string Name { get; init; } = "UdpEndpointPool";
    public int Count => _connPool.Count;
    public int InUse => _connPool.InUse;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) throw new InvalidOperationException("UdpEndpointPool is already closed");

        _stopped = true;
        await _connPool.StopAsync(timeout);
    }
}