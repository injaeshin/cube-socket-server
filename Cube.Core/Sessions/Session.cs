using Microsoft.Extensions.Logging;
using Cube.Common.Interface;
using Cube.Core.Network;

namespace Cube.Core.Sessions;

public interface ISessionExecutor
{
    void Bind(ITransport transport);
    void Run();
}

public interface ISessionNotify
{
    void OnNotifyConnected();
    void OnNotifyDisconnected(bool isGraceful);
    void OnNotifyError(Exception exception);
    Task OnNotifyReceived(ushort packetType, ReadOnlyMemory<byte> payload, byte[]? rentedBuffer);
}

public class Session : ISession, ISessionExecutor, ISessionNotify
{
    private readonly ILogger _logger;
    private ITransport? _transport;

    private readonly string _sessionId = CreateSessionId();
    public string SessionId => _sessionId;

    private readonly SessionKeepAliveHandler _keepAlive;
    private readonly SessionResourceHandler _resource;
    private readonly SessionIOHandler _io;

    #region ISessionState
    private readonly SessionState _state = new();
    public SessionState State => _state;
    public bool IsConnected => _state.IsConnected;
    public bool IsAuthenticated => _state.IsAuthenticated;
    public bool IsDisconnected => _state.IsDisconnected;
    public void SetConnected() => _state.SetConnected();
    public void SetAuthenticated() => _state.SetAuthenticated();
    public void SetDisconnected() => _state.SetDisconnected();
    #endregion    

    protected Session(ILoggerFactory loggerFactory, SessionEventHandler events)
    {
        _logger = loggerFactory.CreateLogger<Session>();
        _keepAlive = events.KeepAlive;
        _resource = events.Resource;
        _io = events.IO;

        _transport = null;
    }

    private static string CreateSessionId()
    {
        string shortTime = DateTime.Now.ToString("HHmm");
        string randomPart = Guid.NewGuid().ToString("N")[..4];
        return $"s_{shortTime}_{randomPart}";
    }

    public void Bind(ITransport transport)
    {
        _transport = transport;
        _transport.BindNotify(this);
    }

    public void Run()
    {
        if (_transport == null)
        {
            throw new Exception("TCP 트랜스포트가 바인딩되지 않았습니다.");
        }

        _transport.Run();
    }

    public void Close(ISessionClose reason)
    {
        _logger.LogDebug("Session Close: {SessionId} - 이유: {Code}", SessionId, reason.Code);
        _transport?.Close(reason.IsGraceful);
    }

    protected void HeartbeatRegister() => _keepAlive.OnRegister(this);
    protected void HeartbeatUnregister() => _keepAlive.OnUnregister(SessionId);
    protected void HeartbeatUpdate() => _keepAlive.OnUpdate(SessionId);
    protected void RemoveSession() => _resource.OnReturnSession(this);

    #region 이벤트 및 패킷 처리
    protected virtual void OnConnected(ISession session)
    {
        _logger.LogDebug("OnConnected: {SessionId}", SessionId);
        HeartbeatRegister();
        SetConnected();
    }

    protected virtual void OnDisconnected(ISession session, bool isGraceful)
    {
        _logger.LogDebug("OnDisconnected: {SessionId} - isGraceful: {isGraceful}", SessionId, isGraceful);
        HeartbeatUnregister();
        RemoveSession();
        _state.Clear();
    }

    protected virtual bool OnPreProcessReceivedAsync(ushort packetType, ReadOnlyMemory<byte> payload)
    {
        _logger.LogDebug("OnPreProcessReceivedAsync: {SessionId}", SessionId);
        HeartbeatUpdate();

        if (packetType == 0x0001/*Ping*/ || packetType == 0x0002/*Pong*/)
        {
            return false;
        }

        return true;
    }

    public async Task SendAsync(Memory<byte> data, byte[]? rentedBuffer)
    {
        if (_transport == null) throw new Exception("TCP 트랜스포트가 바인딩되지 않았습니다.");

        _logger.LogDebug("SendAsync: {SessionId}, length: {Length}", SessionId, data.Length);
        await _io.OnPacketSendAsync(SessionId, data, rentedBuffer, _transport.GetSocket());
    }

    public void OnNotifyConnected()
    {
        OnConnected(this);
    }

    public void OnNotifyDisconnected(bool isGraceful)
    {
        OnDisconnected(this, isGraceful);
    }

    public async Task OnNotifyReceived(ushort packetType, ReadOnlyMemory<byte> payload, byte[]? rentedBuffer)
    {
        if (OnPreProcessReceivedAsync(packetType, payload))
        {
            _logger.LogDebug("OnNotifyReceived: {SessionId}, length: {Length}", SessionId, payload.Length);
            var context = new ReceivedContext(SessionId, packetType, payload, rentedBuffer);
            await _io.OnPacketReceived(context);
        }
    }

    public void OnNotifyError(Exception exception)
    {
        _logger.LogError(exception, "OnError: {SessionId}", SessionId);
    }
    #endregion
}
