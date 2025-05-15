using System.Collections.Concurrent;
using System.Net.Sockets;
using Common.Network.Pool;
using Microsoft.Extensions.Logging;

namespace Common.Network.Session;

public interface INetSessionManager
{
    bool CreateSession(Socket socket);

    void Run();
    void Stop();
}

public abstract class NetSessionManager<T> : INetSessionManager where T : INetSession, INetSessionTransport
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, T> _sessions = new();

    private readonly TcpTransportPool _transportPool;
    private readonly SessionHeartbeat _heartbeatMonitor;

    private CancellationTokenSource? _cts;

    public NetSessionManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<NetSessionManager<T>>();
        _transportPool = new TcpTransportPool(loggerFactory);
        _heartbeatMonitor = new SessionHeartbeat(loggerFactory.CreateLogger<SessionHeartbeat>());
    }

    protected abstract T CreateNewSession(Socket socket, SessionEvents events);

    public void Run()
    {
        _cts = new CancellationTokenSource();
        _heartbeatMonitor.StartAsync(_cts.Token).Wait();
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _heartbeatMonitor.StopAsync(_cts.Token).Wait();
            _cts = null!;
        }

        _transportPool.Close();
    }

    public bool CreateSession(Socket socket)
    {
        if (_sessions.Count >= NetConsts.MAX_CONNECTION)
        {
            _logger.LogWarning("세션 생성 실패: 최대 접속 수 초과");
            return false;
        }

        try
        {
            var session = CreateNewSession(socket, CreateSessionEvents());
            _sessions.TryAdd(session.SessionId, session);

            var transport = _transportPool.Rent(socket);
            session.BindTransport(transport);
            session.Run();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 생성 중 오류 발생");
            return false;
        }
    }

    protected SessionEvents CreateSessionEvents()
    {
        return new SessionEvents
        {
            Resource = new SessionResource
            {
                OnReturnSession = (session) =>
                {
                    _sessions.TryRemove(session.SessionId, out _);
                }
            },
            KeepAlive = new SessionKeepAlive
            {
                OnRegister = _heartbeatMonitor.RegisterSession,
                OnUnregister = _heartbeatMonitor.UnregisterSession,
                OnUpdate = _heartbeatMonitor.UpdateSessionActivity
            }
        };
    }
}
