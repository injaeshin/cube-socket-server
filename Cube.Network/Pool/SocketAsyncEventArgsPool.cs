using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Cube.Network.Pool;

public class SocketAsyncEventArgsPool(ILoggerFactory loggerFactory, int maxConnections)
{
    private readonly BufferPool _eventArgsBuffer = new(maxConnections * NetConsts.BUFFER_SIZE, NetConsts.BUFFER_SIZE, loggerFactory);
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool = new(() => new SocketAsyncEventArgs(), maxConnections);
    private readonly ILogger _logger = loggerFactory.CreateLogger<SocketAsyncEventArgsPool>();

    private bool _closed = false;

    public int Count => _eventArgsPool.Count;

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

        if (!MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
        {
            _logger.LogError("Failed to get array from Memory<byte>");
            return null;
        }

        args.SetBuffer(segment.Array, segment.Offset, segment.Count);

        return args;
    }

    public SocketAsyncEventArgs? RentWithoutBuffer()
    {
        var args = _eventArgsPool.Rent();
        if (args.AcceptSocket != null)
        {
            args.AcceptSocket = null;
        }

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

    public void Close()
    {
        if (_closed) throw new InvalidOperationException("SocketAsyncEventArgsPool is closed");
        _closed = true;

        _eventArgsPool.Close();
        _eventArgsBuffer.Close();
    }
}