using Common.Network.Session;

namespace Server.Chat.Users;

public interface IUser
{
    string Name { get; }
    ISession Session { get; }
}

public class User : IUser
{
    public string Name { get; }
    public ISession Session { get; }

    public User(string name, ISession session)
    {
        Name = name;
        Session = session;
    }
}