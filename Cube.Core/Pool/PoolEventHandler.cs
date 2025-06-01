using System.Net.Sockets;

namespace Cube.Core.Pool;

public interface IPoolHandler<T>
{
    T? Rent();
    T? RentWithoutBuffer();
    void Return(T item);
    int Count { get; }
}

public class SAEAPoolHandler : IPoolHandler<SocketAsyncEventArgs>
{
    private readonly Func<SocketAsyncEventArgs?> _rent;
    private readonly Func<SocketAsyncEventArgs?> _rentWithoutBuffer;
    private readonly Action<SocketAsyncEventArgs> _return;
    private readonly Func<int> _count;

    public SAEAPoolHandler(SocketAsyncEventArgsPool pool)
    {
        _rent = pool.Rent;
        _rentWithoutBuffer = pool.RentWithoutBuffer;
        _return = pool.Return;
        _count = () => pool.Count;
    }

    public SocketAsyncEventArgs? Rent() => _rent();
    public SocketAsyncEventArgs? RentWithoutBuffer() => _rentWithoutBuffer();
    public void Return(SocketAsyncEventArgs item) => _return(item);
    public int Count => _count();
}

