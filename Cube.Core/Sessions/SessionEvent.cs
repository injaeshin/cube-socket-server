using System.Net.Sockets;
using Cube.Network.Context;

namespace Cube.Core.Sessions;

public class SessionResourceEvent
{
    public required Action<ISession> OnReturnSession;
}

public class SessionKeepAliveEvent
{
    public required Action<ISession> OnRegister;
    public required Action<string> OnUnregister;
    public required Action<string> OnUpdate;
}

public class SessionIOEvent
{
    public required Func<string, Memory<byte>, byte[]?, Socket, Task> OnSendEnqueueAsync;
    public required Func<string, ReadOnlyMemory<byte>, byte[]?, Task> OnReceived;
}

public class SessionEvent
{
    public required SessionResourceEvent Resource;
    public required SessionKeepAliveEvent KeepAlive;
    public required SessionIOEvent IO;
}



