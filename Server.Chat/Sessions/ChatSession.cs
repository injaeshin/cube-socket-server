using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;
using Common.Network.Transport;
using Common.Network.Packet;
using Common.Network.Handler;

namespace Server.Chat.Sessions;

public class ChatSession : Session, ISession
{
    private readonly ILogger _logger;
    private readonly SessionEvents _events;
    private readonly IPacketDispatcher _packetDispatcher;

    private readonly ReceiveChannel _recvChannel;
    private readonly SendChannel _sendChannel;

    public ChatSession(ILoggerFactory loggerFactory, IPacketDispatcher packetDispatcher, SessionEvents events) : base(loggerFactory.CreateLogger<Session>())
    {
        _logger = loggerFactory.CreateLogger<ChatSession>();
        _packetDispatcher = packetDispatcher;
        _events = events;

        _recvChannel = new ReceiveChannel(loggerFactory.CreateLogger<ReceiveChannel>());
        _sendChannel = new SendChannel();
    }
    

    protected override void OnConnected(ISession session)
    {
        _logger.LogInformation("세션 연결됨: {SessionId}", SessionId);
        _sendChannel.Run(Socket);
        _events.KeepAlive.OnRegister(this);
    }

    protected override void OnDisconnected(ISession session, DisconnectReason reason)
    {
        _logger.LogInformation("세션 연결 끊김: {SessionId} - 이유: {Reason}", SessionId, reason);
        _sendChannel.Stop();
        _events.KeepAlive.OnUnregister(SessionId);
        _events.Resource.OnReturnSession(this);
    }

    protected override async Task OnPacketReceived(ReceivedPacket packet)
    {
        _events.KeepAlive.OnUpdate(SessionId);

        // 선행 처리
        if (packet.Type == PacketType.Ping || packet.Type == PacketType.Pong)
        {
            return;
        }

        if (!_packetDispatcher.IsRegistered(packet.Type))
        {
            _logger.LogError("패킷 처리 중단: {SessionId} - 이유: {Type}", SessionId, packet.Type);
            Close(DisconnectReason.InvalidData);
            return;
        }

        await _recvChannel.EnqueueAsync(packet);
    }

    public override async Task OnProcessReceivedPacketAsync(ReceivedPacket packet)
    {
        _logger.LogDebug("[Session Process Received] {SessionId} - Length: {DataLength}", SessionId, packet.Data.Length);

        if (!_packetDispatcher.TryGetHandler(packet.Type, out var handler))
        {
            _logger.LogError("패킷 처리 중단: {SessionId} - 이유: {Reason}", SessionId, packet.Data.Length);
            Close(DisconnectReason.InvalidData);
            return;
        }

        await handler.HandleAsync(this, packet.Data);
    }

    public override async Task SendAsync(ReadOnlyMemory<byte> payload)
    {
        await _sendChannel.EnqueueAsync(payload);
    }
}