using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;
using Common.Network.Packet;
using Common.Network.Handler;
using Server.Chat.Helper;

namespace Server.Chat.Session;

public interface IChatSession : INetSession
{
}

public class ChatSession : NetSession, IChatSession
{
    private readonly ILogger _logger;
    private readonly IPacketDispatcher _packetDispatcher;

    public ChatSession(IPacketDispatcher packetDispatcher, SessionEvents events) : base(LoggerFactoryHelper.GetLoggerFactory(), events)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatSession>();
        _packetDispatcher = packetDispatcher;
    }

    protected override void OnConnected(INetSession session)
    {
        _logger.LogInformation("ChatSession connected: {SessionId}", SessionId);
        HeartbeatRegister();
    }

    protected override void OnDisconnected(INetSession session, bool isGraceful)
    {
        _logger.LogInformation("ChatSession disconnected: {SessionId}", SessionId);
        HeartbeatUnregister();
        RemoveSession();
    }

    protected override bool OnPreProcessReceivedAsync(ReceivedPacket packet)
    {
        HeartbeatUpdate();

        if (packet.Type == MessageType.Ping || packet.Type == MessageType.Pong)
        {
            return false;
        }

        return true;
    }

    public override async Task OnProcessReceivedAsync(ReceivedPacket packet)
    {
        if (!_packetDispatcher.TryGetHandler(packet.Type, out var handler))
        {
            _logger.LogError("Invalid packet type: {SessionId} - {Type} / Length: {Length}", SessionId, packet.Type, packet.Data.Length);
            Close(DisconnectReason.InvalidType);
            return;
        }

        await handler.HandleAsync(this, packet.Data);
    }
}