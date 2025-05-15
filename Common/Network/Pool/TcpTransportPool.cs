using System.Diagnostics;
using System.Net.Sockets;
using Common.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Common.Network.Pool;

public class TcpTransportPool
{
    private readonly ObjectPool<ITransport> _transportPool;
    private readonly SocketAsyncEventArgsPool _receiveArgsPool;

    public TcpTransportPool(ILoggerFactory loggerFactory)
    {
        _receiveArgsPool = new SocketAsyncEventArgsPool(loggerFactory, NetConsts.MAX_CONNECTION);
        _transportPool = new ObjectPool<ITransport>(() => new TcpTransport(loggerFactory, Return, Return), NetConsts.MAX_CONNECTION);
    }

    public ITransport Rent(Socket socket)
    {
        var transport = _transportPool.Rent() ?? throw new InvalidOperationException("트랜스포트 대여 실패");
        var receiveArgs = _receiveArgsPool.Rent() ?? throw new InvalidOperationException("수신 버퍼 대여 실패");

        transport.Reset();
        transport.BindSocket(socket, receiveArgs);
        return transport;
    }

    public void Return(ITransport transport)
    {
        if (transport == null) throw new ArgumentNullException(nameof(transport));
        _transportPool.Return(transport);
    }

    public void Return(SocketAsyncEventArgs receiveArgs)
    {
        if (receiveArgs == null) throw new ArgumentNullException(nameof(receiveArgs));
        _receiveArgsPool.Return(receiveArgs);
    }

    public (int, int) GetCount()
    {
        return (_transportPool.Count, _receiveArgsPool.Count);
    }

    public void Close()
    {
        Debug.WriteLine($"TransportPool : {_transportPool.Count} / ReceiveArgsPool : {_receiveArgsPool.Count}");

        _transportPool.Dispose();
        _receiveArgsPool.Dispose();
    }
}