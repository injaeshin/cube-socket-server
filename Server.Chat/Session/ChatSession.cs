using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;
using Common.Network.Packet;
using Common.Network.Handler;
using Server.Chat.Helper;

namespace Server.Chat.Sessions;

public class ChatSession(IPacketDispatcher packetDispatcher, SessionEvents events) : Session(LoggerFactoryHelper.Instance.GetLoggerFactory()), ISession
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<ChatSession>();
    private readonly SessionEvents _events = events;
    private readonly IPacketDispatcher _packetDispatcher = packetDispatcher;

    protected override void OnConnected(ISession session)
    {
        base.OnConnected(session);
        _events.KeepAlive.OnRegister(this);
    }

    protected override void OnDisconnected(ISession session, DisconnectReason reason)
    {
        base.OnDisconnected(session, reason);
        _events.KeepAlive.OnUnregister(SessionId);
        _events.Resource.OnReturnSession(this);
    }

    protected override async Task OnPacketReceived(ReceivedPacket packet)
    {
        await base.OnPacketReceived(packet);
        _events.KeepAlive.OnUpdate(SessionId);

        // 선행 처리
        if (packet.Type == MessageType.Ping || packet.Type == MessageType.Pong)
        {
            return;
        }

        if (!_packetDispatcher.IsRegistered(packet.Type))
        {
            _logger.LogError("패킷 처리 중단: {SessionId} - 이유: {Type}", SessionId, packet.Type);
            Close(DisconnectReason.InvalidData);
            return;
        }

        await EnqueueRecvAsync(packet);
    }

    public override async Task OnProcessReceivedPacketAsync(ReceivedPacket packet)
    {
        await base.OnProcessReceivedPacketAsync(packet);
        _logger.LogDebug("[Session Process Received] {SessionId} - Length: {DataLength}", SessionId, packet.Data.Length);

        if (!_packetDispatcher.TryGetHandler(packet.Type, out var handler))
        {
            _logger.LogError("패킷 처리 중단: {SessionId} - 이유: {Reason}", SessionId, packet.Data.Length);
            Close(DisconnectReason.InvalidData);
            return;
        }

        await handler.HandleAsync(this, packet.Data);
    }

    public override async Task SendAsync(ReadOnlyMemory<byte> packet)
    {
        await base.SendAsync(packet);
        await EnqueueSendAsync(packet);
    }
}