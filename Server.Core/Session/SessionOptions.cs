using System.Net.Sockets;
using Common.Transport;
using Server.Core.Packet;

namespace Server.Core.Session;

public class SessionResource
{
    public required Func<SocketAsyncEventArgs?> OnRentRecvArgs;
    public required Action<SocketAsyncEventArgs> OnReturnRecvArgs;
    public required Action<ISocketSession> OnReturnSession;
}

public class SessionQueue
{
    public required Func<ReceivedPacket, Task> OnRecvEnqueueAsync;
    public required Func<SendRequest, Task> OnSendEnqueueAsync;
}

public class SocketSessionOptions
{
    public required SessionResource Resource;
    public required SessionQueue Queue;
}
