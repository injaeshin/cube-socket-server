using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;
using Common.Network.Handler;
using Server.Chat.Helper;

namespace Server.Chat.Session;

public interface ISessionManager
{
    bool CreateSession(Socket socket);
    bool TryGetSession(string sessionId, out ISession session);
    void End();
}

public class SessionManager : ISessionManager
{
    private SessionPool _sessionPool = null!;
    private readonly ILogger<SessionManager> _logger;
    private readonly IPacketDispatcher _packetDispatcher;
    private readonly SocketEventArgsPool _receiveArgsPool;
    private readonly SessionHeartbeat _heartbeatMonitor;
    private CancellationTokenSource? _cts;

    public SessionManager(SocketEventArgsPool receiveArgsPool, SessionHeartbeat heartbeatMonitor, IPacketDispatcher packetDispatcher)
    {
        _logger = LoggerFactoryHelper.Instance.CreateLogger<SessionManager>();

        _receiveArgsPool = receiveArgsPool;
        _heartbeatMonitor = heartbeatMonitor;
        _packetDispatcher = packetDispatcher;

        Initialize();
    }

    // 별도 초기화 메서드
    private void Initialize()
    {
        _sessionPool = new SessionPool(SessionFactory);

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

        _receiveArgsPool.Close();
        _sessionPool.Close();
    }

    // 세션 팩토리 메서드
    private ISession SessionFactory()
    {
        var events = new SessionEvents
        {
            Resource = new SessionResource
            {
                OnReturnSession = Return
            },
            KeepAlive = new SessionKeepAlive
            {
                OnRegister = _heartbeatMonitor.RegisterSession,
                OnUnregister = _heartbeatMonitor.UnregisterSession,
                OnUpdate = _heartbeatMonitor.UpdateSessionActivity
            }
        };

        return new ChatSession(_packetDispatcher, events);
    }

    public bool CreateSession(Socket socket)
    {
        if (_sessionPool.IsMaxConnection())
        {
            _logger.LogWarning("세션 생성 실패: 최대 접속 수 초과");
            return false;
        }

        try
        {
            if (!_sessionPool.TryRent(out var activeSession))
            {
                _logger.LogWarning("세션 대여 실패: 사용 가능한 세션 없음");
                return false;
            }

            var receiveArgs = _receiveArgsPool.Rent();
            if (receiveArgs == null)
            {
                _logger.LogWarning("세션 대여 실패: 사용 가능한 세션 없음");
                return false;
            }

            activeSession.Run(socket, receiveArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 대여 중 오류 발생");
            return false;
        }

        return true;
    }

    private void Return(ISession session)
    {
        try
        {
            if (!_packetDispatcher.TryGetHandler(MessageType.Logout, out var handler))
            {
                _logger.LogError("Logout 패킷 핸들러를 찾을 수 없습니다.");
                return;
            }

            handler!.HandleAsync(session, ReadOnlyMemory<byte>.Empty);
            _receiveArgsPool.Return(session.ReceiveArgs);
            _sessionPool.Return(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 반환 중 오류 발생: {SessionId}", session?.SessionId);
        }
    }

    public bool TryGetSession(string sessionId, out ISession session)
    {
        try
        {
            session = _sessionPool.GetSession(sessionId) ?? null!;
            return session != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 조회 중 오류 발생: {SessionId}", sessionId);
            session = null!;
            return false;
        }
    }
}