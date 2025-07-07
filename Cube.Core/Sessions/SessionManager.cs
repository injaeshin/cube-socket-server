using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using Cube.Core.Network;
using Cube.Core.Router;
using Cube.Packet;
using Cube.Core.Settings;
using Cube.Packet.Builder;

namespace Cube.Core.Sessions;

public interface ISessionManager
{
    void Run();
    void Close();

    Task SendToAll(Memory<byte> data, byte[]? rentedBuffer);
    void Kick(string sessionId, ErrorType reason);

    bool TryGetSession(string sessionId, out ISession session);
}

public abstract class SessionManager<T> : ISessionManager where T : ICoreSession
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IFunctionRouter _functionRouter;
    private readonly IHeartbeat _heartbeatMonitor;

    private readonly ConcurrentDictionary<string, T> _sessions = new();
    private volatile bool _running = false;

    private readonly Timer _resendTimer;
    private readonly NetworkConfig _networkConfig;
    private readonly HeartbeatConfig _heartbeatConfig;

    public SessionManager(ILoggerFactory loggerFactory, IFunctionRouter functionRouter, IHeartbeat heartbeatMonitor, ISettingsService settingsService)
    {
        _logger = loggerFactory.CreateLogger<SessionManager<T>>();
        _loggerFactory = loggerFactory;
        _functionRouter = functionRouter;
        _heartbeatMonitor = heartbeatMonitor;
        _networkConfig = settingsService.Network;
        _heartbeatConfig = settingsService.Heartbeat;

        _resendTimer = new Timer(
            _ => ResendUnacked(),
            null,
            _heartbeatConfig.ResendIntervalMs,
            _heartbeatConfig.ResendIntervalMs
        );

        _functionRouter.AddFunc<TcpConnectedCmd, bool>(cmd => OnTcpClientConnected(cmd.Connection));
        _functionRouter.AddFunc<UdpConnectedCmd, bool>(cmd => OnUdpClientConnected(cmd.SessionId, cmd.Sequence, cmd.Connection));

        _functionRouter.AddAction<UdpReceivedCmd>(cmd => OnUdpReceived(cmd.Context));
        _functionRouter.AddAction<UdpReceivedAckCmd>(cmd => OnUdpAckReceived(cmd.SessionId, cmd.Ack));
        _functionRouter.AddAction<UdpTrackSentCmd>(cmd => OnUdpTrackSent(cmd.Context));

        _functionRouter.AddAction<SessionReturnCmd>(cmd => DeleteSession(cmd.SessionId));
    }

    protected abstract T CreateSession(ILoggerFactory logger, IHeartbeat heartbeat, IFunctionRouter functionRouter);

    public void Run()
    {
        if (_running) throw new InvalidOperationException("SessionManager is already running");
        _running = true;
    }

    public void Close()
    {
        if (!_running) throw new InvalidOperationException("SessionManager is not running");
        _running = false;

        _heartbeatMonitor.Close();
        _resendTimer.Dispose();

        foreach (var session in _sessions.Values)
        {
            session.Kick(ErrorType.ApplicationRequest);
        }

        _sessions.Clear();
    }

    public void Kick(string sessionId, ErrorType reason)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        session.Kick(reason);
    }

    private void DeleteSession(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public async Task SendToAll(Memory<byte> data, byte[]? rentedBuffer)
    {
        foreach (var session in _sessions.Values)
        {
            var (newData, newRentedBuffer) = PacketHelper.CopyTo(data);
            await session.SendAsync(newData, newRentedBuffer);
        }
    }

    public bool OnTcpClientConnected(ITcpConnection conn)
    {
        if (!_running) throw new InvalidOperationException("SessionManager is not running");

        if (_sessions.Count >= _networkConfig.MaxConnections)
        {
            _logger.LogWarning("세션 생성 실패: 최대 접속 수 초과");
            return false;
        }

        try
        {
            var session = CreateSession(_loggerFactory, _heartbeatMonitor, _functionRouter);
            if (!_sessions.TryAdd(session.SessionId, session))
            {
                _logger.LogWarning("세션 생성 실패: {SessionId}", session.SessionId);
                return false;
            }

            session.Bind(conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 생성 중 오류 발생");
            return false;
        }

        return true;
    }

    public bool OnUdpClientConnected(string sessionId, ushort seq, IUdpConnection conn)
    {
        if (!_running) throw new InvalidOperationException("SessionManager is not running");

        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("세션 존재 하지 않음: {SessionId}", sessionId);
                return false;
            }

            if (session.IsDisconnected)
            {
                _logger.LogWarning("세션 연결 끊김: {SessionId}", sessionId);
                return false;
            }

            // 이미 존재하는 세션인지 확인
            if (session.UdpConnection != null)
            {
                session.CloseUdpConnection();
            }

            session.Bind(conn);
            session.UdpConnection!.InitExpectedSeqence(seq);

            var (data, rentedBuffer) = new PacketWriter(PacketType.Welcome).ToUdpPacket();
            _ = session.SendAsync(data, rentedBuffer, TransportType.Udp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "세션 생성 중 오류 발생");
            return false;
        }

        return true;
    }


    public void OnUdpAckReceived(string sessionId, ushort ack)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        if (session.IsDisconnected || session.UdpConnection == null)
        {
            _logger.LogWarning("세션 연결 끊김: {SessionId}", sessionId);
            return;
        }

        session.UdpConnection.Acknowledge(ack);
    }

    public void OnUdpTrackSent(UdpSendContext sendContext)
    {
        if (!_sessions.TryGetValue(sendContext.SessionId, out var session))
        {
            return;
        }

        if (session.IsDisconnected || session.UdpConnection == null)
        {
            _logger.LogWarning("세션 연결 끊김: {SessionId}", sendContext.SessionId);
            return;
        }

        session.UdpConnection.Track(sendContext);
    }

    public void OnUdpReceived(UdpReceivedContext ctx)
    {
        if (!_running) throw new InvalidOperationException("SessionManager is not running");

        if (ctx.PacketType == PacketType.Ack)
        {
            OnUdpAckReceived(ctx.SessionId, ctx.Ack);
            return;
        }

        if (!_sessions.TryGetValue(ctx.SessionId, out var session))
        {
            _logger.LogWarning("세션 존재 하지 않음: {SessionId}", ctx.SessionId);
            return;
        }

        if (session.IsDisconnected || session.UdpConnection == null)
        {
            _logger.LogWarning("세션 연결 끊김: {SessionId}", ctx.SessionId);
            return;
        }

        session.UdpConnection.UpdateReceived(ctx);
    }

    public bool TryGetSession(string sessionId, out ISession session)
    {
        session = default!;
        if (!_sessions.TryGetValue(sessionId, out var ss))
        {
            return false;
        }

        session = ss;
        return true;
    }

    private void ResendUnacked()
    {
        if (!_running) return;

        var now = DateTime.UtcNow;
        foreach (var session in _sessions.Values)
        {
            if (session.IsDisconnected || session.UdpConnection == null) continue;

            session.UdpConnection.ResendUnacked(now);
        }
    }
}
