using System.Collections.Concurrent;

using Common;
using Server.Core.Session;
using Server.Core.Pool;

namespace Server.Core.Manager;

public class SessionPool
{
    private readonly ObjectPool<ISocketSession> _pool;
    private readonly ConcurrentDictionary<string, ISocketSession> _sessions;
    public SessionPool(Func<ISocketSession> factory)
    {
        _pool = new(factory, Constants.MAX_CONNECTION);
        _sessions = new ConcurrentDictionary<string, ISocketSession>();
    }

    public bool TryRent(out ISocketSession? session)
    {
        session = _pool.Rent();
        session.CreateSessionId();
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            _pool.Return(session);
            return false;
        }

        return true;
    }

    public ISocketSession? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void Return(ISocketSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);
        _pool.Return(session);
    }

    public bool IsMaxConnection()
    {
        return _sessions.Count >= Constants.MAX_CONNECTION;
    }
}
