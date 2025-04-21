using System.Net.Sockets;

namespace Common.Network.Pool;

public class SocketEventArgsPool
{
    private readonly BufferPool _eventArgsBuffer;
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;

    public int Count => _eventArgsPool.Count;

    public SocketEventArgsPool()
    {
        _eventArgsBuffer = new BufferPool(Constant.MAX_CONNECTION * Constant.BUFFER_SIZE, Constant.BUFFER_SIZE);
        _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() =>
        {
            var args = new SocketAsyncEventArgs();
            return args;
        }, Constant.MAX_CONNECTION);
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
