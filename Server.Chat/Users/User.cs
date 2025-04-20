using Server.Core.Session;

namespace Server.Chat.Users;

public interface IUser
{
    string Name { get; }
    ISocketSession Session { get; }
}

public class User : IUser
{
    public string Name { get; }
    public ISocketSession Session { get; }

    public User(string name, ISocketSession session)
    {
        Name = name;
        Session = session;
    }
}