using System.Net.Sockets;
using Cube.Packet;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Pool;

public class SocketAsyncEventArgsPool(ILoggerFactory loggerFactory, int maxConnections) : IAsyncResource
{
    private readonly BufferPool _bufferPool = new(loggerFactory, maxConnections * PacketConsts.BUFFER_SIZE, PacketConsts.BUFFER_SIZE);
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool = new(() => new SocketAsyncEventArgs(), maxConnections);
    private volatile bool _stopped = false;

    public SocketAsyncEventArgs? Rent()
    {
        if (_stopped) throw new ObjectDisposedException(nameof(SocketAsyncEventArgsPool));

        var args = _eventArgsPool.Rent();
        ClearEventArgs(args);

        var buffer = _bufferPool.AllocateBuffer();
        if (buffer.IsEmpty)
        {
            _eventArgsPool.Return(args); // 메모리 실패 시에도 반환
            return null;
        }

        args.SetBuffer(buffer);
        return args;
    }

    public SocketAsyncEventArgs? RentWithoutBuffer()
    {
        if (_stopped) throw new ObjectDisposedException(nameof(SocketAsyncEventArgsPool));

        var args = _eventArgsPool.Rent();
        ClearEventArgs(args);
        args.SetBuffer(Memory<byte>.Empty);
        return args;
    }

    public void Return(SocketAsyncEventArgs args)
    {
        ClearEventArgs(args);
        _eventArgsPool.Return(args);
    }

    private void ClearEventArgs(SocketAsyncEventArgs args)
    {
        args.AcceptSocket = null;

        if (!args.MemoryBuffer.IsEmpty)
        {
            _bufferPool.FreeBuffer(args.MemoryBuffer);
            args.SetBuffer(Memory<byte>.Empty);
        }

        args.UserToken = null;
        args.RemoteEndPoint = null;
    }

    public string Name { get; init; } = "SocketAsyncEventArgsPool";
    public int Count => _eventArgsPool.Count;
    public int InUse => _eventArgsPool.InUse;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) throw new InvalidOperationException("SocketAsyncEventArgsPool is already closed");
        _stopped = true;

        await _eventArgsPool.StopAsync(timeout);
        await _bufferPool.StopAsync(timeout);
    }
}