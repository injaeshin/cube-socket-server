
using Cube.Core;

namespace Cube.Server.Chat.User;

public interface IChatUser
{
    string Name { get; }
    ISession Session { get; }
}

public class ChatUser(string name, ISession session) : IChatUser
{
    public string Name { get; } = name;
    public ISession Session { get; } = session;
}