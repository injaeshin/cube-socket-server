using System.Collections.Concurrent;
using Common.Network.Session;
using Server.Chat.Channel;

namespace Server.Chat.User;

public interface IChatUser
{
    string Name { get; }
    INetSession Session { get; }
}

public class ChatUser(string name, INetSession session) : IChatUser
{
    public string Name { get; } = name;
    public INetSession Session { get; } = session;

    public ConcurrentDictionary<int, ChatChannel> Channels { get; } = new();

    public async Task<bool> TryAddChannel(int channelId, ChatChannel channel)
    {
        if (Channels.TryAdd(channelId, channel))
        {
            await channel.TryAddMember(this);
            return true;
        }

        return false;
    }

    public async Task<bool> TryRemoveChannel(int channelId)
    {
        if (Channels.TryRemove(channelId, out var channel))
        {
            await channel.TryRemoveMember(this);
            return true;
        }

        return false;
    }

    public bool TryGetChannel(int channelId, out ChatChannel? channel)
    {
        return Channels.TryGetValue(channelId, out channel);
    }

    public IEnumerable<ChatChannel> GetAllChannels()
    {
        return Channels.Values;
    }
}