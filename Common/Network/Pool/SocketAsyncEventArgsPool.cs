using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Common.Network.Pool;

public class SocketAsyncEventArgsPool : IDisposable
{
    private readonly BufferPool _eventArgsBuffer;
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;
    private readonly ILogger _logger;
    private bool _disposed = false;

    public int Count => _eventArgsPool.Count;

    public SocketAsyncEventArgsPool(ILoggerFactory loggerFactory, int maxConnections)
    {
        _logger = loggerFactory.CreateLogger<SocketAsyncEventArgsPool>();
        _eventArgsBuffer = new BufferPool(maxConnections * NetConsts.BUFFER_SIZE, NetConsts.BUFFER_SIZE, loggerFactory);
        _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() => new SocketAsyncEventArgs(), maxConnections);
    }

    public SocketAsyncEventArgs? Rent()
    {
        if (_eventArgsPool.Count <= 0)
        {
            _logger.LogCritical("풀 소진");
            return null;
        }

        var buffer = _eventArgsBuffer.AllocateBuffer();
        if (buffer.IsEmpty)
        {
            _logger.LogCritical("버퍼 소진");
            return null;
        }

        var args = _eventArgsPool.Rent();
        if (args.AcceptSocket != null)
        {
            args.AcceptSocket = null;
        }

        if (args.Buffer != null)
        {
            _eventArgsBuffer.FreeBuffer(args.Buffer);
            args.SetBuffer(null, 0, 0);
        }

        args.SetBuffer(buffer.ToArray(), 0, buffer.Length);

        return args;
    }

    public void Return(SocketAsyncEventArgs args)
    {
        if (args.AcceptSocket != null)
        {
            args.AcceptSocket = null;
        }

        if (args.Buffer != null)
        {
            var buffer = new Memory<byte>(args.Buffer, args.Offset, args.Count);
            _eventArgsBuffer.FreeBuffer(buffer);
            args.SetBuffer(null, 0, 0);
        }

        _eventArgsPool.Return(args);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _eventArgsBuffer.Dispose();
        _eventArgsPool.Dispose();
        _disposed = true;
    }
}