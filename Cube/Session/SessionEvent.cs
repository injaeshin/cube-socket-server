using System.Net.Sockets;

namespace Cube.Session;

public class SessionResourceEvent
{
    public required Action<INetSession> OnReturnSession;
}

public class SessionKeepAliveEvent
{
    public required Action<INetSession> OnRegister;
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



