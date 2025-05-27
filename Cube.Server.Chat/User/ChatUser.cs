using System.Collections.Concurrent;
using Cube.Common.Interface;
using Cube.Server.Chat.Channel;

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