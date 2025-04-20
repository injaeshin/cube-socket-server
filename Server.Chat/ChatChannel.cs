//using System.Collections.Concurrent;
//using Common.Protocol;

//namespace Server.Chat;

//public class ChatChannel
//{
//    public string ChannelId { get; }
//    private readonly ConcurrentDictionary<string, User> _members = new();

//    public ChatChannel(string channelId)
//    {
//        ChannelId = channelId;
//    }

//    public void AddMember(User user)
//    {
//        _members.TryAdd(user.UserName, user);
//    }

//    public void RemoveMember(User user)
//    {
//        _members.TryRemove(user.UserName, out _);
//    }

//    public IEnumerable<User> GetAllMembers()
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
