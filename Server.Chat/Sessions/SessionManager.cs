using Microsoft.Extensions.Logging;

using Common.Network.Session;
using Common.Network.Pool;
using Common.Network.Queue;
using Server.Chat.Users;
using Common.Network.Packet;

namespace Server.Chat.Sessions;

public interface ISessionManager
{
    void Begin(UserManagerAction userManagerAction);
    void End();

    bool TryRent(out ISession? session);
    void Return(ISession session);
    ISession? GetSession(string sessionId);
    bool IsMaxConnection();
}

public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private SessionPool _sessionPool = null!;
    private UserManagerAction _userManagerAction = null!;
    private readonly ReceiveQueue _recvQueue;
    private readonly SendQueue _sendQueue;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SocketEventArgsPool _receiveArgsPool;
    private readonly SessionHeartbeat _heartbeatMonitor;
    private CancellationTokenSource? _cts;

    public SessionManager(SocketEventArgsPool receiveArgsPool, ILoggerFactory loggerFactory, SessionHeartbeat heartbeatMonitor)
    {
        _logger = loggerFactory.CreateLogger<SessionManager>();

        // 필요한 의존성 저장
        _loggerFactory = loggerFactory;
        _receiveArgsPool = receiveArgsPool;
        _heartbeatMonitor = heartbeatMonitor;

        // 1단계: 기본 구성요소 초기화
        _sendQueue = new SendQueue();
        _recvQueue = new ReceiveQueue(loggerFactory.CreateLogger<ReceiveQueue>());
    }

    // 별도 초기화 메서드
    public void Begin(UserManagerAction userManagerAction)
    {
        _userManagerAction = userManagerAction;
        _sessionPool = new SessionPool(CreateUserSession);

        _cts = new CancellationTokenSource();
        _heartbeatMonitor.StartAsync(_cts.Token).Wait();
    }

    public void End()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _heartbeatMonitor.StopAsync(_cts.Token).Wait();
            _cts = null!;
        }
    }

    // 세션 팩토리 메서드
    private Session CreateUserSession()
    {
        var options = new SocketSessionOptions
        {
            Resource = new SessionResource
            {
                OnRentRecvArgs = _receiveArgsPool.Rent,
                OnReturnRecvArgs = _receiveArgsPool.Return,
                OnReturnSession = Return
            },
            Queue = new SessionQueue
            {
                OnSendEnqueueAsync = _sendQueue.EnqueueAsync,
                OnRecvEnqueueAsync = _recvQueue.EnqueueAsync
            }
        };

        var session = new Session(options, _loggerFactory);
        session.SessionConnected += OnSessionConnected;
        session.SessionDisconnected += OnSessionDisconnected;
        session.SessionDataReceived += OnSessionReceivedPacket;

        return session;
    }

    private void OnSessionConnected(object? sender, SessionEventArgs e)
    {
        _heartbeatMonitor.RegisterSession(e.Session.SessionId, e.Session);
    }

    private void OnSessionDisconnected(object? sender, SessionEventArgs e)
    {
        _heartbeatMonitor.UnregisterSession(e.Session.SessionId);
    }

    private void OnSessionReceivedPacket(object? sender, SessionDataEventArgs e)
    {
        _logger.LogDebug("[Session Received] {SessionId} - {PacketType}", e.Session.SessionId, e.PacketType);

        _heartbeatMonitor.UpdateSessionActivity(e.Session.SessionId);

        switch (e.PacketType)
        {
            case PacketType.Pong:
                return;
            case PacketType.Login:
                _userManagerAction.OnInsert(e.Session.SessionId, e.Session);
                e.Session.SendAsync(PacketType.Ping, Array.Empty<byte>());
                break;
            default:
                break;
        }

        // if (e.PacketType == PacketType.Login)
        // {
        // }
    }

    // ISessionManager 인터페이스 구현
    public bool TryRent(out ISession? session)
    {
        try
        {
            var result = _sessionPool.TryRent(out var activeSession);
            session = activeSession;
            return result;
        }
        catch (Exception ex)
        {
            session = null;
            _logger.LogError(ex, "세션 대여 중 오류 발생");
            return false;
        }
    }

    public void Return(ISession session)
    {
        try
        {
            _userManagerAction.OnDelete(session.SessionId);
            _sessionPool.Return(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 반환 중 오류 발생: {SessionId}", session?.SessionId);
        }
    }

    public ISession? GetSession(string sessionId)
    {
        try
        {
            return _sessionPool.GetSession(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 조회 중 오류 발생: {SessionId}", sessionId);
            return null;
        }
    }

    public bool IsMaxConnection()
    {
        return _sessionPool.IsMaxConnection();
    }
}