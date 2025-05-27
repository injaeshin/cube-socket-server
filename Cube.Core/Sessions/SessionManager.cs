using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Cube.Common.Interface;
using Cube.Core.Network;
using Cube.Common;

namespace Cube.Core.Sessions;

public interface ISessionCreator
{
    bool CreateAndRunSession(Socket socket, ITransport transport);
}

public interface ISessionManager : ISessionCreator
{
    // bool TryGetSessionId(EndPoint ep, out string sessionId);
    bool TryGetSession(string sessionId, out ISession? session);

    // bool CreateUdpSession(string sessionId, EndPoint ep, Func<EndPoint, ushort, ReadOnlyMemory<byte>, Task> udpSendToAsync);
    // bool TryGetUdpSession(string sessionId, out UdpSessionState? udpSession);
    // void RemoveUdpSession(string sessionId);

    // void SetEndPointToSessionId(EndPoint ep, string sessionId);
    // void RemoveEndPointToSessionId(EndPoint ep);

    void Run(SessionIOHandler sessionIOEvent);
    //void Stop();
}

public abstract class SessionManager<T>(ILoggerFactory loggerFactory, SessionHeartbeat heartbeatMonitor) : ISessionManager, ISessionCreator where T : ISession, ISessionExecutor
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionManager<T>>();
    private readonly SessionHeartbeat _heartbeatMonitor = heartbeatMonitor;
    private readonly ConcurrentDictionary<string, T> _sessions = new();

    // private readonly ConcurrentDictionary<string, UdpSessionState> _UdpSessions = new();
    // private readonly ConcurrentDictionary<EndPoint, string> _endPointToSessionId = new();

    private SessionEventHandler _eventHandler = null!;
    protected abstract T CreateNewSession(Socket socket, SessionEventHandler events);
    private bool _isRunning = false;

    public void Run(SessionIOHandler sessionIOEvent)
    {
        if (_isRunning) throw new InvalidOperationException("SessionManager is already running");
        _eventHandler = CreateSessionEvents(sessionIOEvent);
        _isRunning = true;
    }

    public bool CreateAndRunSession(Socket socket, ITransport transport)
    {
        if (!_isRunning) throw new InvalidOperationException("SessionManager is not running");

        if (_sessions.Count >= Consts.MAX_CONNECTIONS)
        {
            _logger.LogWarning("세션 생성 실패: 최대 접속 수 초과");
            return false;
        }

        try
        {
            var session = CreateNewSession(socket, _eventHandler);
            _sessions.TryAdd(session.SessionId, session);

            session.Bind(transport);
            session.Run();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 생성 중 오류 발생");
            return false;
        }

        return true;
    }

    private SessionEventHandler CreateSessionEvents(SessionIOHandler ioEvent)
    {
        return new SessionEventHandler
        {
            Resource = new SessionResourceHandler
            {
                OnReturnSession = (session) =>
                {
                    _eventHandler.IO.OnSessionClosed(session.SessionId);
                    _sessions.TryRemove(session.SessionId, out _);
                }
            },
            KeepAlive = new SessionKeepAliveHandler
            {
                OnRegister = _heartbeatMonitor.RegisterSession,
                OnUnregister = _heartbeatMonitor.UnregisterSession,
                OnUpdate = _heartbeatMonitor.UpdateSessionActivity
            },
            IO = ioEvent
        };
    }

    // public bool TryGetSessionId(EndPoint ep, out string sessionId)
    // {
    //     sessionId = _endPointToSessionId.GetValueOrDefault(ep, String.Empty);
    //     if (string.IsNullOrEmpty(sessionId))
    //     {
    //         _logger.LogWarning("세션 ID 조회 실패: {EndPoint}", ep);
    //         return false;
    //     }

    //     return true;
    // }

    public bool TryGetSession(string sessionId, out ISession? session)
    {
        if (!_sessions.TryGetValue(sessionId, out var s))
        {
            session = null;
            return false;
        }

        session = s;
        return true;
    }

    // public bool TryGetUdpSession(string sessionId, out UdpSessionState? udpSession)
    // {
    //     if (!_UdpSessions.TryGetValue(sessionId, out var s))
    //     {
    //         udpSession = null;
    //         return false;
    //     }

    //     udpSession = s;
    //     return true;
    // }

    // public bool CreateUdpSession(string sessionId, EndPoint ep, Func<EndPoint, ushort, ReadOnlyMemory<byte>, Task> udpSendToAsync)
    // {
    //     if (!_UdpSessions.TryAdd(sessionId, new UdpSessionState(sessionId, ep, udpSendToAsync)))
    //     {
    //         _logger.LogWarning("UDP 세션 생성 실패: {SessionId}", sessionId);
    //         return false;
    //     }

    //     return true;
    // }

    // public void RemoveUdpSession(string sessionId) => _UdpSessions.TryRemove(sessionId, out _);
    // public void SetEndPointToSessionId(EndPoint ep, string sessionId) => _endPointToSessionId[ep] = sessionId;
    // public void RemoveEndPointToSessionId(EndPoint ep) => _endPointToSessionId.TryRemove(ep, out _);
}
