using Cube.Core.Network;
using Cube.Core.Router;
using Cube.Packet;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Sessions;

public class Session : ICoreSession, INotifySession
{
    private readonly ILogger _logger;
    private readonly ISessionHeartbeat _heartbeat;
    private readonly IFunctionRouter _functionRouter;

    private readonly string _sessionId;
    public string SessionId => _sessionId;

    private ITcpConnection? _tcpConnection = null;
    private IUdpConnection? _udpConnection = null;

    protected Session(ILogger<Session> logger, IHeartbeat heartbeat, IFunctionRouter callbackRouter)
    {
        _logger = logger;
        _heartbeat = heartbeat;
        _functionRouter = callbackRouter;
        _sessionId = SessionHelper.CreateSessionId();
    }

    #region ISessionState
    private readonly SessionState _state = new();
    public bool IsConnected => _state.IsConnected;
    public bool IsAuthenticated => _state.IsAuthenticated;
    public bool IsDisconnected => _state.IsDisconnected;
    public void SetAuthenticated() => _state.SetAuthenticated();
    private void SetConnected() => _state.SetConnected();
    private void SetDisconnected() => _state.SetDisconnected();
    #endregion

    #region ICoreSession
    public void Bind(ITcpConnection conn)
    {
        _tcpConnection = conn;
        _tcpConnection.BindNotify(this);
        _tcpConnection.Run();
    }

    public void Bind(IUdpConnection conn)
    {
        _udpConnection = conn;
        _udpConnection.BindNotify(this);
        _udpConnection.Run();
    }

    public ITcpConnection? TcpConnection => _tcpConnection;
    public IUdpConnection? UdpConnection => _udpConnection;

    public void CloseUdpConnection()
    {
        if (_udpConnection == null)
        {
            return;
        }

        _udpConnection.Close();
        _udpConnection = null;
    }
    #endregion

    #region 이벤트 핸들러
    protected virtual void OnConnected(ISession session, TransportType transportType)
    {
        _logger.LogDebug("OnConnected: {SessionId}", SessionId);
        Welcome();
    }

    protected virtual void OnDisconnected(ISession session, TransportType transportType, bool isGraceful)
    {
        _logger.LogDebug("OnDisconnected: {SessionId} - isGraceful: {isGraceful} {transportType}", SessionId, isGraceful, transportType);
        Cleanup(ErrorType.GracefulDisconnect);
        ReturnSession();
    }

    protected virtual bool OnPreProcessReceivedAsync(PacketType packetType)
    {
        _logger.LogDebug("OnPreProcessReceivedAsync: {SessionId}, PacketType: {PacketType}", SessionId, packetType);
        HeartbeatUpdate();

        if (packetType == PacketType.Ping || packetType == PacketType.Pong)
        {
            return false;
        }

        return true;
    }

    public void OnNotifyConnected(TransportType transportType)
    {
        OnConnected(this, transportType);
    }

    public void OnNotifyDisconnected(TransportType transportType, bool isGraceful)
    {
        OnDisconnected(this, transportType, isGraceful);
    }

    public async Task OnNotifyUdpSend(UdpSendContext context)
    {
        await _functionRouter.InvokeFunc<UdpSendCmd, Task>(new UdpSendCmd(context));
    }

    public async Task OnNotifyReceived(ReceivedContext context)
    {
        if (!OnPreProcessReceivedAsync(context.PacketType)) return;
        _logger.LogDebug("OnNotifyReceived: {SessionId}, length: {Length}", SessionId, context.Payload.Length);

        await _functionRouter.InvokeFunc<ReceivedEnqueueCmd, Task>(new ReceivedEnqueueCmd(context));
    }

    public void OnNotifyError(TransportType transportType, Exception exception)
    {
        _logger.LogError(exception, "OnError: {TransportType} {SessionId}", transportType, SessionId);
    }

    protected void HeartbeatRegister() => _heartbeat!.RegisterSession(this);
    protected void HeartbeatUnregister() => _heartbeat!.UnregisterSession(SessionId);
    protected void HeartbeatUpdate() => _heartbeat!.UpdateSessionActivity(SessionId);
    protected void ReturnSession() => _functionRouter.InvokeAction<SessionReturnCmd>(new SessionReturnCmd(SessionId));
    #endregion

    public void Kick(ErrorType reason)
    {
        _logger.LogDebug("Session Kick: {Id} - Reason: {Desc}", SessionId, reason);
        Cleanup(reason);
    }

    public async Task SendAsync(Memory<byte> data, byte[]? rentedBuffer, TransportType transportType = TransportType.Tcp)
    {
        switch (transportType)
        {
            case TransportType.Tcp:
                {
                    if (_tcpConnection == null) throw new Exception("TCP 트랜스포트가 바인딩되지 않았습니다.");

                    //_logger.LogDebug($"TCP Send: HexDump: {PacketHelper.ToHexDump(data)}");
                    var ctx = new TcpSendContext(SessionId, data, rentedBuffer, _tcpConnection.GetSocket());
                    await _functionRouter.InvokeFunc<TcpSendCmd, Task>(new TcpSendCmd(ctx));
                }
                break;
            case TransportType.Udp:
                {
                    if (_udpConnection == null) throw new Exception("UDP 트랜스포트가 바인딩되지 않았습니다.");

                    var seq = _udpConnection!.NextSequence();
                    if (!data.SetUdpHeader(SessionId, seq))
                    {
                        _logger.LogError("UDP 패킷 헤더 설정 실패: {SessionId}, {Seq}, {Ack}", SessionId, seq, 0);
                        return;
                    }

                    //_logger.LogDebug($"UDP Send: HexDump: {PacketHelper.ToHexDump(data)}");
                    var ctx = new UdpSendContext(SessionId, data, rentedBuffer, _udpConnection.GetEndPoint(), seq);
                    await _functionRouter.InvokeFunc<UdpSendCmd, Task>(new UdpSendCmd(ctx));
                }
                break;
            default: throw new Exception("Invalid transport type");
        }
    }

    private void Welcome()
    {
        if (IsConnected)
        {
            return;
        }

        SetConnected();
        HeartbeatRegister();

        _logger.LogDebug("Welcome: {SessionId}", SessionId);
    }

    private void Cleanup(ErrorType reason)
    {
        if (IsDisconnected) return;
        SetDisconnected();

        HeartbeatUnregister();
        _tcpConnection?.Close();
        _udpConnection?.Close();

        _functionRouter.InvokeFunc<ReceivedEnqueueCmd, Task>(
                        new ReceivedEnqueueCmd(new ReceivedContext(SessionId, PacketType.Logout, Memory<byte>.Empty, null)));

        _logger.LogDebug("Cleanup: {SessionId} - Reason: {Desc}", SessionId, reason);
    }
}