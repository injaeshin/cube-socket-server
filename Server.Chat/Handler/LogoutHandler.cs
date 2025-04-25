using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Handler;
using Common.Network.Session;
using Server.Chat.Users;


namespace Server.Chat.Handler;

public class LogoutHandler(ILogger<LogoutHandler> logger, IUserManager userManager) : IPacketHandler
{
    private readonly ILogger _logger = logger;
    private readonly IUserManager _userManager = userManager;

    public MessageType Type => MessageType.Logout;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        _userManager.DeleteUser(session.SessionId);
        _logger.LogInformation("Logout: {SessionId}", session.SessionId);

        await Task.CompletedTask;
        return true;
    }
}