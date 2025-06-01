using System.Net;
using System.Net.Sockets;
using Cube.Core.Network;

namespace Cube.Core.Sessions;

public class SessionResourceHandler
{
    public required Action<ISession> OnReturnSession;
}

public class SessionKeepAliveHandler
{
    public required Action<ISession> OnRegister;
    public required Action<string> OnUnregister;
    public required Action<string> OnUpdate;
}

public class SessionIOHandler
{
    public required Func<string, Memory<byte>, byte[]?, Socket, Task> OnTcpSendAsync;
    public required Func<string, Memory<byte>, byte[]?, EndPoint, ushort, ushort, Task> OnUdpSendAsync;
    public required Func<ReceivedContext, Task> OnPacketReceivedAsync;
}

public class SessionStateHandler
{
    public required Func<string, Task> OnClosedAsync;
}
