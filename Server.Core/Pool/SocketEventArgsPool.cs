using System.Net.Sockets;

using Common;

namespace Server.Core.Pool;

// public interface ISocketEventArgsPool
// {
//     public SocketAsyncEventArgs Rent { get; }
//     public void Return(SocketAsyncEventArgs args);
// }

public class SocketEventArgsPool
{
    private readonly BufferPool _eventArgsBuffer;
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;

    public int Count => _eventArgsPool.Count;

    public SocketEventArgsPool()
    {
        _eventArgsBuffer = new BufferPool(Constants.MAX_CONNECTION * Constants.BUFFER_SIZE, Constants.BUFFER_SIZE);
        _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() =>
        {
            var args = new SocketAsyncEventArgs();
            return args;
        }, Constants.MAX_CONNECTION);
    }

    public SocketAsyncEventArgs? Rent()
    {
        try
        {
            var buffer = _eventArgsBuffer.AllocateBuffer();
            var args = _eventArgsPool.Rent();
            args.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            return args;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Return(SocketAsyncEventArgs args)
    {
        _eventArgsBuffer.FreeBuffer(args.Buffer!);
        _eventArgsPool.Return(args);
    }

    public void Close()
    {
        _eventArgsBuffer.Close();
        _eventArgsPool.Close();
    }
}
