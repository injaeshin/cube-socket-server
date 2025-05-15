using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Transport;
using Common.Network.Buffer;

namespace Common.Network.Session;

public interface INetSessionTransport
{
    ITransport Transport { get; }
    void BindTransport(ITransport transport);
}

public interface INetSessionAuthenticate
{
    bool IsAuthenticated { get; }
    void Authenticate();
    void Deauthenticate();
}

public interface INetSession : INetSessionAuthenticate
{
    string SessionId { get; }
    bool IsConnectionAlive();

    void Run();
    void Close(DisconnectReason reason);
    Task SendAsync(ReadOnlyMemory<byte> packet);
    Task OnProcessReceivedAsync(ReceivedPacket packet);
}

public class NetSession : INetSession, INetSessionTransport, ITransportNotify
{
    private readonly ILogger _logger;
    private readonly ReceiveChannel _processChannel;

    private readonly string _sessionId = CreateSessionId();
    public string SessionId => _sessionId;
    private ITransport _transport = null!;
    public ITransport Transport => _transport;

    private bool _isAuthenticated = false;
    public bool IsAuthenticated => _isAuthenticated;
    public void Authenticate() => _isAuthenticated = true;
    public void Deauthenticate() => _isAuthenticated = false;

    private readonly SessionKeepAlive _keepAlive;
    private readonly SessionResource _resource;

    protected NetSession(ILoggerFactory loggerFactory, SessionEvents events)
    {
        _logger = loggerFactory.CreateLogger<NetSession>();
        _processChannel = new(loggerFactory.CreateLogger<ReceiveChannel>());
        _keepAlive = events.KeepAlive;
        _resource = events.Resource;
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
        try
        {
            _logger.LogDebug("[Session Run] 세션 시작: {SessionId}", SessionId);
            _processChannel.Run();
            _transport.Run();

            OnConnected(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Session Run] 세션 시작 중 오류 발생: {SessionId}", SessionId);
            throw;
        }
    }

    public void Close(DisconnectReason reason)
    {
        _logger.LogDebug("Session Close: {SessionId} - 이유: {Reason}", SessionId, reason);

        _transport?.Close();
        _processChannel.Reset();
    }

    public bool IsConnectionAlive() => _transport.IsConnectionAlive();

    public async Task EnqueueRecvAsync(ReceivedPacket packet) => await _processChannel.EnqueueAsync(packet);


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

    protected virtual bool OnPreProcessReceivedAsync(ReceivedPacket packet)
    {
        _logger.LogDebug("OnPreProcessReceivedAsync: {SessionId}", SessionId);
        HeartbeatUpdate();
        return true;
    }

    public virtual async Task OnProcessReceivedAsync(ReceivedPacket packet)
    {
        _logger.LogDebug("OnProcessReceivedPacketAsync: {SessionId}", SessionId);
        await Task.CompletedTask;
    }

    public virtual async Task SendAsync(ReadOnlyMemory<byte> packet)
    {
        _logger.LogDebug("SendAsync: {SessionId}, length: {Length}", SessionId, packet.Length);
        await _transport.SendAsync(packet);
    }

    public void OnNotifyConnected()
    {
        OnConnected(this);
    }

    public void OnNotifyDisconnected(bool isGraceful)
    {
        OnDisconnected(this, isGraceful);
    }

    public async Task OnNotifyReceived(MessageType packetType, ReadOnlyMemory<byte> packet, byte[]? rentedBuffer)
    {
        var receivedPacket = new ReceivedPacket(packetType, packet, rentedBuffer, this);
        if (OnPreProcessReceivedAsync(receivedPacket))
        {
            await EnqueueRecvAsync(receivedPacket);
        }
    }

    public void OnNotifyError(Exception exception)
    {
        _logger.LogError(exception, "OnError: {SessionId}", SessionId);
    }
    #endregion
}