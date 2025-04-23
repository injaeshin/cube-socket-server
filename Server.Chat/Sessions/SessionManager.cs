using Microsoft.Extensions.Logging;

using Common.Network.Session;
using Common.Network.Pool;
using Common.Network.Queue;
using Common.Network.Packet;
using Common.Network.Handler;

namespace Server.Chat.Sessions;

public interface ISessionManager
{
    void End();

    bool TryRent(out ISession? session);
    void Return(ISession session);
    ISession? GetSession(string sessionId);
    bool IsMaxConnection();
}

public class SessionManager : ISessionManager
{
    private SessionPool _sessionPool = null!;

    private readonly ILogger<SessionManager> _logger;
    private readonly ReceiveQueue _recvQueue;
    private readonly SendQueue _sendQueue;
    private readonly IPacketDispatcher _packetDispatcher;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SocketEventArgsPool _receiveArgsPool;
    private readonly SessionHeartbeat _heartbeatMonitor;
    private CancellationTokenSource? _cts;

    public SessionManager(SocketEventArgsPool receiveArgsPool, ILoggerFactory loggerFactory,
                            SessionHeartbeat heartbeatMonitor, IPacketDispatcher packetDispatcher)
    {
        _logger = loggerFactory.CreateLogger<SessionManager>();

        // 필요한 의존성 저장
        _loggerFactory = loggerFactory;
        _receiveArgsPool = receiveArgsPool;
        _heartbeatMonitor = heartbeatMonitor;
        _packetDispatcher = packetDispatcher;

        // 1단계: 기본 구성요소 초기화
        _sendQueue = new SendQueue();
        _recvQueue = new ReceiveQueue(loggerFactory.CreateLogger<ReceiveQueue>());

        Initialize();
    }

    // 별도 초기화 메서드
    private void Initialize()
    {
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

        var session = new Session(options, _loggerFactory, _packetDispatcher);
        session.SessionConnected += OnSessionConnected;
        session.SessionDisconnected += OnSessionDisconnected;
        session.SessionPreProcess += OnSessionPreProcess;

        return session;
    }

    private void OnSessionConnected(object? sender, SessionEventArgs e)
    {
        _logger.LogInformation("[Session Connected] {SessionId}", e.Session.SessionId);
        _heartbeatMonitor.RegisterSession(e.Session.SessionId, e.Session);
    }

    private void OnSessionDisconnected(object? sender, SessionEventArgs e)
    {
        _logger.LogInformation("[Session Disconnected] {SessionId}", e.Session.SessionId);
        _heartbeatMonitor.UnregisterSession(e.Session.SessionId);
    }

    private async Task<SessionPreProcessResult> OnSessionPreProcess(object? sender, SessionDataEventArgs e)
    {
        _logger.LogDebug("[Session Received] {SessionId}", e.Session.SessionId);

        _heartbeatMonitor.UpdateSessionActivity(e.Session.SessionId);

        var packetType = PacketIO.GetPacketType(e.Data);
        if (packetType == PacketType.Ping || packetType == PacketType.Pong)
        {
            return SessionPreProcessResult.Handled;
        }

        await Task.CompletedTask;
        return SessionPreProcessResult.Continue;
    }

    // ISessionManager 인터페이스 구현
    public bool TryRent(out ISession? session)
    {
        try
        {
            var result = _sessionPool.TryRent(out var activeSession);
            session = activeSession;
            
            if (!result || session == null)
            {
                _logger.LogWarning("세션 대여 실패: 사용 가능한 세션 없음");
                return false;
            }
            
            return true;
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
            if (!_packetDispatcher.TryGetHandler(PacketType.Logout, out var handler))
            {
                _logger.LogError("Logout 패킷 핸들러를 찾을 수 없습니다.");
                return;
            }

            handler!.HandleAsync(session, ReadOnlyMemory<byte>.Empty);
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