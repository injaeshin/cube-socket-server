using Microsoft.Extensions.Logging;
using Cube.Common.Shared;
using Cube.Network;
using Cube.Network.Context;
using Cube.Network.PacketIO;
using Cube.Network.Transport;


namespace Cube.Session;

public interface INetSessionTransport
{
    void BindTransport(ITransport transport);
}

public interface INetSession
{
    string SessionId { get; }
    bool IsConnectionAlive();

    void Run();
    void Close(DisconnectReason reason);
    Task SendAsync(PacketWriter packetWriter);
    Task OnNotifyReceived(ReadOnlyMemory<byte> packet, byte[]? rentedBuffer);

    bool IsAuthenticated { get; }
    void Authenticate();
    void Deauthenticate();
}

public class NetSession : INetSession, INetSessionTransport, ITransportNotify
{
    private readonly ILogger _logger;
    private ITransport? _transport;

    private readonly string _sessionId = CreateSessionId();
    public string SessionId => _sessionId;

    private bool _isAuthenticated = false;
    public bool IsAuthenticated => _isAuthenticated;
    public void Authenticate() => _isAuthenticated = true;
    public void Deauthenticate() => _isAuthenticated = false;

    private readonly SessionKeepAliveEvent _keepAlive;
    private readonly SessionResourceEvent _resource;
    private readonly SessionIOEvent _io;

    protected NetSession(ILoggerFactory loggerFactory, SessionEvent events)
    {
        _logger = loggerFactory.CreateLogger<NetSession>();
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

    public void BindTransport(ITransport transport)
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

    public void Close(DisconnectReason reason)
    {
        _logger.LogDebug("Session Close: {SessionId} - 이유: {Reason}", SessionId, reason);

        _transport?.Close();
    }

    public bool IsConnectionAlive() => _transport?.IsConnectionAlive() == true;

    protected void HeartbeatRegister() => _keepAlive.OnRegister(this);
    protected void HeartbeatUnregister() => _keepAlive.OnUnregister(SessionId);
    protected void HeartbeatUpdate() => _keepAlive.OnUpdate(SessionId);
    protected void RemoveSession() => _resource.OnReturnSession(this);

    #region 이벤트 및 패킷 처리
    protected virtual void OnConnected(INetSession session)
    {
        _logger.LogDebug("OnConnected: {SessionId}", SessionId);
        HeartbeatRegister();
    }

    protected virtual void OnDisconnected(INetSession session, bool isGraceful)
    {
        _logger.LogDebug("OnDisconnected: {SessionId} - isGraceful: {isGraceful}", SessionId, isGraceful);
        RemoveSession();
        HeartbeatUnregister();
    }

    protected virtual bool OnPreProcessReceivedAsync(ReadOnlyMemory<byte> payload)
    {
        _logger.LogDebug("OnPreProcessReceivedAsync: {SessionId}", SessionId);
        HeartbeatUpdate();

        if (!PacketHelper.TryGetPacketType(payload, out var type))
        {
            _logger.LogError("OnPreProcessReceivedAsync: {SessionId} - 패킷 타입 파싱 실패", SessionId);
            return false;
        }

        var packetType = (PacketType)type;
        if (packetType == PacketType.Ping || packetType == PacketType.Pong)
        {
            return false;
        }

        return true;
    }

    public async Task SendAsync(PacketWriter packetWriter)
    {
        _logger.LogDebug("SendAsync: {SessionId}, length: {Length}", SessionId, packetWriter.Length);
        if (_transport == null) throw new Exception("TCP 트랜스포트가 바인딩되지 않았습니다.");

        var (data, rentedBuffer) = packetWriter.ToTcpPacket();
        await _io.OnSendEnqueueAsync(SessionId, data, rentedBuffer, _transport.GetSocket());
    }

    public void OnNotifyConnected()
    {
        OnConnected(this);
    }

    public void OnNotifyDisconnected(bool isGraceful)
    {
        OnDisconnected(this, isGraceful);
    }

    public async Task OnNotifyReceived(ReadOnlyMemory<byte> payload, byte[]? rentedBuffer)
    {
        if (OnPreProcessReceivedAsync(payload))
        {
            await _io.OnReceived(SessionId, payload, rentedBuffer);
        }
    }

    public void OnNotifyError(Exception exception)
    {
        _logger.LogError(exception, "OnError: {SessionId}", SessionId);
    }
    #endregion
}