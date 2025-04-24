//using Server.Chat.Users;
//using System.Collections.Concurrent;

//namespace Server.Chat;

//public class ChatChannel
//{
//    public string ChannelId { get; }
//    private readonly ConcurrentDictionary<string, IUser> _members = new();

//    public ChatChannel(string channelId)
//    {
//        ChannelId = channelId;
//    }

//    public void AddMember(IUser user)
//    {
//        _members.TryAdd(user.Name, user);
//    }

//    public void RemoveMember(IUser user)
//    {
//        _members.TryRemove(user.Name, out _);
//    }

//    public IEnumerable<IUser> GetAllMembers()
//    {
//        return _members.Values;
//    }

//    public async Task SendMessageTo(string sender, string message, string target)
//    {
//        if (_members.TryGetValue(target, out var member))
//        {
//            await SendMessageAsync(member, sender, message);
//        }
//    }

//    public async Task SendMessageToAll(string sender, string message)
//    {
//        foreach (var member in _members.Values)
//        {
//            if (member.UserName == sender)
//            {
//                continue;
//            }

//            if (!member.Session.IsConnected)
//            {
//                continue;
//            }

//            await SendMessageAsync(member, sender, message);
//        }
//    }

//    private async Task SendMessageAsync(User target, string sender, string message)
//    {
//        using var packet = PacketWriter.Create();
//        packet.Write(sender);
//        packet.Write(message);
//        await target.Session.SendAsync(PacketType.ChatMessage, packet.ToMemory());
//    }
//}
