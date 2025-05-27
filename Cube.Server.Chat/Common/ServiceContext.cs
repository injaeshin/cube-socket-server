using Cube.Core.Sessions;
using Cube.Server.Chat.User;

namespace Cube.Server.Chat;

public interface IServiceContext
{
    ISessionManager SessionManager { get; }
    IUserManager UserManager { get; }
}

public class ServiceContext : IServiceContext
{
    public ISessionManager SessionManager { get; init; }
    public IUserManager UserManager { get; init; }

    public ServiceContext(ISessionManager sessionManager, IUserManager userManager)
    {
        SessionManager = sessionManager;
        UserManager = userManager;
    }
}
