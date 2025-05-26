using Microsoft.Extensions.Logging;

using Cube.Network;
using Cube.Core.Handler;
using Cube.Core.Sessions;
using Cube.Common.Shared;
using Cube.Server.Chat.Helper;
using Cube.Server.Chat.Service;

namespace Cube.Server.Chat.Handler;

public class LogoutHandler(IChatService chatService) : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<LogoutHandler>();
    private readonly IChatService _chatService = chatService;

    public PacketType Type => PacketType.Logout;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        if (!session.IsAuthenticated)
        {
            _logger.LogError("Logout packet received from unauthenticated session: {SessionId}", session.SessionId);
            session.Close(DisconnectReason.NotAuthenticated);
            return false;
        }

        _logger.LogInformation("Logout: {SessionId}", session.SessionId);
        await _chatService.DeleteUser(session.SessionId);
        session.Deauthenticate();

        return true;
    }
}