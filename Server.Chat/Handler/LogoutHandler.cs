using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Handler;
using Common.Network.Session;
using Server.Chat.User;
using Server.Chat.Helper;
using Server.Chat.Service;


namespace Server.Chat.Handler;

public class LogoutHandler(IChatService chatService) : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<LogoutHandler>();
    private readonly IChatService _chatService = chatService;

    public MessageType Type => MessageType.Logout;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        _logger.LogInformation("Logout: {SessionId}", session.SessionId);
        await _chatService.DeleteUser(session.SessionId);

        return true;
    }
}