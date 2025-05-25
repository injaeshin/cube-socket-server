using System.Net.Sockets;

namespace Cube.Network.Pool;

public class PoolEvent
{
    public required Func<SocketAsyncEventArgs?> OnRentEventArgs;
    public required Action<SocketAsyncEventArgs> OnReleaseEventArgs;
    public required Func<int> OnGetCount;
}

