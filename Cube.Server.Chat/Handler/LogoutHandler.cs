using Microsoft.Extensions.Logging;
using Cube.Common.Interface;
using Cube.Packet;
using Cube.Server.Chat.Helper;
using Cube.Server.Chat.Service;


namespace Cube.Server.Chat.Handler;

public class LogoutHandler(IChatService chatService) : PacketHandlerBase
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<LogoutHandler>();
    private readonly IChatService _chatService = chatService;

    public override PacketType Type => PacketType.Logout;

    public override async Task<bool> HandleAsync(ISession session)
    {
        _logger.LogInformation("Logout: {SessionId}", session.SessionId);
        session.SetDisconnected();
        await _chatService.DeleteUser(session.SessionId);

        return true;
    }
}