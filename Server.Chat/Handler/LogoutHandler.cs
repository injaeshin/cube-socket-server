using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Handler;
using Common.Network.Session;
using Server.Chat.User;


namespace Server.Chat.Handler;

public class LogoutHandler(IUserManager userManager) : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<LogoutHandler>();
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