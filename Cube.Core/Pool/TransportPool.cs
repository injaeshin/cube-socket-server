using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Network;

namespace Cube.Core.Pool;

public class TransportPool
{
    private readonly ObjectPool<ITransport> _transportPool;
    private readonly PoolEvent _poolEvent;
    private bool _closed = false;

    public TransportPool(ILoggerFactory loggerFactory, PoolEvent poolEvent, int poolSize = CoreConsts.MAX_CONNECTIONS)
    {
        _poolEvent = poolEvent;
        var returnEvent = new TransportReturnHandler
        {
            OnReleaseTransport = Return,
            OnReleaseReceiveArgs = poolEvent.OnReleaseEventArgs,
        };

        _transportPool = new ObjectPool<ITransport>(() => new TcpTransport(loggerFactory, returnEvent), poolSize);
    }

    public ITransport Rent(Socket socket)
    {
        var transport = _transportPool.Rent() ?? throw new InvalidOperationException("TCP 풀 미생성");
        var args = _poolEvent.OnRentEventArgs() ?? throw new InvalidOperationException("수신 버퍼 대여 실패");
        transport.BindSocket(socket, args);

        return transport;
    }

    public void Return(ITransport transport)
    {
        _transportPool.Return(transport);
    }

    public (int, int) GetCount()
    {
        return (_transportPool.Count, _poolEvent.OnGetCount());
    }

    public void Close()
    {
        if (_closed) throw new InvalidOperationException("TransportPool is closed");
        _closed = true;

        _transportPool.Close();
    }
}