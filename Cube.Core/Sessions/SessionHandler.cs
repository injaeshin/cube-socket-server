using System.Net.Sockets;
using Cube.Common.Interface;
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
    public required Func<string, Memory<byte>, byte[]?, Socket, Task> OnPacketSendAsync;
    public required Func<ReceivedContext, Task> OnPacketReceived;
    public required Func<string, Task> OnSessionClosed;
}

public class SessionEventHandler
{
    public required SessionResourceHandler Resource;
    public required SessionKeepAliveHandler KeepAlive;
    public required SessionIOHandler IO;
}



