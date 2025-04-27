using System.Collections.Concurrent;
using Common.Network;
using Common.Network.Packet;
using Server.Chat.User;

namespace Server.Chat.Channel;

public class ChatChannel(int channelId, string channelName)
{
    //private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<ChatChannel>();
    private readonly ConcurrentDictionary<string, IChatUser> _members = new();

    private readonly int _channelId = channelId;
    private readonly string _channelName = channelName;
    public int ChannelId => _channelId;
    public string ChannelName => _channelName;

    public int Count => _members.Count;

    public async Task<bool> TryAddMember(IChatUser user)
    {
        if (!_members.TryAdd(user.Name, user))
        {
            return false;
        }

        using var packet = new PacketWriter();
        PacketWriteMessage(packet, user.Name, $"{user.Name} 님이 채널에 참여했습니다.");

        foreach (var member in _members.Values)
        {
            if (member.Name == user.Name)
            {
                continue;
            }

            if (!member.Session.IsConnectionAlive())
            {
                continue;
            }

            await SendMessageAsync(member, packet.ToPacket());
        }

        return true;
    }

    public async Task<bool> TryRemoveMember(IChatUser user)
    {
        if (!_members.TryRemove(user.Name, out _))
        {
            return false;
        }

        using var packet = new PacketWriter();
        PacketWriteMessage(packet, user.Name, $"{user.Name} 님이 채널에서 나갔습니다.");

        foreach (var member in _members.Values)
        {
            if (member.Name == user.Name)
            {
                continue;
            }

            if (!member.Session.IsConnectionAlive())
            {
                continue;
            }

            await SendMessageAsync(member, packet.ToPacket());
        }

        return true;
    }

    public IEnumerable<IChatUser> GetAllMembers()
    {
        return _members.Values;
    }

    public async Task SendMessageTo(string sender, string message, string target)
    {
        if (_members.TryGetValue(target, out var member))
        {
            if (!member.Session.IsConnectionAlive())
            {
                return;
            }

            using var packet = new PacketWriter();
            PacketWriteMessage(packet, sender, message);

            await SendMessageAsync(member, packet.ToPacket());
        }
    }

    public async Task SendMessageToAll(string sender, string message)
    {
        using var packet = new PacketWriter();
        PacketWriteMessage(packet, sender, message);

        foreach (var member in _members.Values)
        {
            if (!member.Session.IsConnectionAlive())
            {
                continue;
            }

            await SendMessageAsync(member, packet.ToPacket());
        }
    }

    private static void PacketWriteMessage(PacketWriter packet, string sender, string message)
    {
        packet.WriteType(MessageType.ChatMessage);
        packet.WriteString(sender);
        packet.WriteString(message);
    }

    private static async Task SendMessageAsync(IChatUser target, ReadOnlyMemory<byte> packet)
    {
        await target.Session.SendAsync(packet);
    }
}
