using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;
using Common.Network.Transport;
using Common.Network.Packet;
using Common.Network.Handler;

namespace Server.Chat.Sessions;

public class Session : SocketSession
{
    private readonly ILogger _logger;
    private readonly IPacketDispatcher _packetDispatcher;

    public Session(SocketSessionOptions options,
                    ILoggerFactory loggerFactory,
                    IPacketDispatcher packetDispatcher) : base(options, loggerFactory.CreateLogger<SocketSession>())
    {
        _logger = loggerFactory.CreateLogger<Session>();
        _packetDispatcher = packetDispatcher;
    }

    protected override void OnConnect(ISession session)
    {
        _logger.LogInformation("세션 연결됨: {SessionId}", SessionId);
    }

    protected override void OnDisconnect(ISession session, DisconnectReason reason)
    {
        _logger.LogInformation("세션 연결 끊김: {SessionId} - 이유: {Reason}", SessionId, reason);
        ResetBuffer();
    }

    public override async Task OnProcessReceivedAsync(ReceivedPacket packet)
    {
        _logger.LogDebug("[Session Process Received] {SessionId} - Length: {DataLength}", SessionId, packet.Data.Length);

        var packetType = PacketIO.GetPacketType(packet.Data);
        if (!_packetDispatcher.TryGetHandler(packetType, out var handler))
        {   
            _logger.LogError("패킷 핸들러를 찾을 수 없습니다. {PacketType}", packetType);
        }

        if (!await handler!.HandleAsync(this, packet.Data))
        {
            packet.Session?.Close();
        }
    }
}