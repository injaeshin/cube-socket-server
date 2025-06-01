using Cube.Server.Chat.Service;
using Cube.Server.Chat.Session;
using Cube.Server.Chat.User;

namespace Cube.Server.Chat;

public interface IManagerContext
{
    IChatSessionManager SessionManager { get; }
    IUserManager UserManager { get; }
}

public class ManagerContext(IChatSessionManager sessionManager, IUserManager userManager) : IManagerContext
{
    public IChatSessionManager SessionManager { get; init; } = sessionManager;
    public IUserManager UserManager { get; init; } = userManager;

}
