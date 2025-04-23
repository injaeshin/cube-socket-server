using System.Collections.Concurrent;

using Common.Network.Session;

namespace Common.Network.Pool;

public class SessionPool
{
    private readonly ObjectPool<ISession> _pool;
    private readonly ConcurrentDictionary<string, ISession> _sessions;
    public SessionPool(Func<ISession> factory)
    {
        _pool = new(factory, Constant.MAX_CONNECTION);
        _sessions = new ConcurrentDictionary<string, ISession>();
    }

    public bool TryRent(out ISession? session)
    {
        session = _pool.Rent();
        // 세션 풀에서 대여할 때 패킷 버퍼를 초기화
        session.ResetBuffer();
        session.CreateSessionId();
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            _pool.Return(session);
            return false;
        }

        return true;
    }

    public ISession? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void Return(ISession session)
    {
        _sessions.TryRemove(session.SessionId, out _);
        
        // 세션 반환 전에 패킷 버퍼를 초기화
        session.ResetBuffer();
        
        _pool.Return(session);
    }

    public bool IsMaxConnection()
    {
        return _sessions.Count >= Constant.MAX_CONNECTION;
    }
}
