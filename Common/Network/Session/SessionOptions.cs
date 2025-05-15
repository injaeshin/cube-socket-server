
namespace Common.Network.Session;

public class SessionResource
{
    public required Action<INetSession> OnReturnSession;
}

public class SessionKeepAlive
{
    public required Action<INetSession> OnRegister;
    public required Action<string> OnUnregister;
    public required Action<string> OnUpdate;
}

public class SessionEvents
{
    public required SessionResource Resource;
    public required SessionKeepAlive KeepAlive;
}
