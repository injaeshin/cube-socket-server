using Microsoft.Extensions.Logging;

using Common.Network.Handler;
using Common.Network.Packet;
using Common.Network.Session;
using Server.Chat.Users;

namespace Server.Chat.Handler;

public class LoginHandler(ILogger<LoginHandler> logger, IUserManager userManager) : IPacketHandler
{
    private readonly ILogger _logger = logger;
    private readonly IUserManager _userManager = userManager;

    public PacketType PacketType => PacketType.Login;

    public async Task HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        var reader = new PacketReader(packet);

        var packetType = reader.ReadPacketType();
        if (packetType != PacketType.Login)
        {
            _logger.LogError("Invalid packet type: {PacketType}", packetType);
            return;
        }

        if (!_userManager.InsertUser(session.SessionId, session))
        {
            _logger.LogError("Failed to insert user: {SessionId}", session.SessionId);
            return;
        }

        var id = reader.ReadString();
        var password = reader.ReadString();
        _logger.LogInformation("Login: {Id}, {Password}", id, password);

        await session.SendAsync(PacketType.LoginSuccess, Array.Empty<byte>());
    }
}