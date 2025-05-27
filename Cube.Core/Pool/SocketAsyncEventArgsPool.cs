using System.Net.Sockets;
using Cube.Common;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Pool;

public class SocketAsyncEventArgsPool(ILoggerFactory loggerFactory, int maxConnections)
{
    private readonly BufferPool _eventArgsBuffer = new(loggerFactory, maxConnections * Consts.BUFFER_SIZE, Consts.BUFFER_SIZE);
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool = new(() => new SocketAsyncEventArgs(), maxConnections);
    private readonly ILogger _logger = loggerFactory.CreateLogger<SocketAsyncEventArgsPool>();

    private bool _closed = false;

    public int Count => _eventArgsPool.Count;

    public SocketAsyncEventArgs? Rent()
    {
        var args = _eventArgsPool.Rent();
        ClearEventArgs(args);

        var buffer = _eventArgsBuffer.AllocateBuffer();
        if (buffer.IsEmpty)
        {
            _logger.LogCritical("버퍼 소진");
            return null;
        }

        args.SetBuffer(buffer);
        return args;
    }

    public SocketAsyncEventArgs? RentWithoutBuffer()
    {
        var args = _eventArgsPool.Rent();
        if (args == null)
        {
            _logger.LogCritical("풀 소진");
            return null;
        }

        ClearEventArgs(args);
        args.SetBuffer(null, 0, 0);

        return args;
    }

    public void Return(SocketAsyncEventArgs args)
    {
        ClearEventArgs(args);
        _eventArgsPool.Return(args);
    }

    private void ClearEventArgs(SocketAsyncEventArgs args)
    {
        if (args.AcceptSocket != null)
        {
            args.AcceptSocket = null;
        }

        if (args.Buffer != null)
        {
            _eventArgsBuffer.FreeBuffer(args.Buffer);
            args.SetBuffer(null, 0, 0);
        }
    }

    public void Close()
    {
        if (_closed) throw new InvalidOperationException("SocketAsyncEventArgsPool is closed");
        _closed = true;

        _eventArgsPool.Close();
        _eventArgsBuffer.Close();
    }
}