
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Server.Chat.Channel;
using Server.Chat.Helper;

namespace Server.Chat.Services;

public class ChatService
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<ChatService>();
    private readonly ConcurrentDictionary<int, ChatChannel> _channels = new();

    public ChatService()
    {
    }

    public bool CreateChannel(int channelId, string channelName)
    {
        return _channels.TryAdd(channelId, new ChatChannel(channelId, channelName));
    }

    public bool RemoveChannel(int channelId)
    {
        return _channels.TryRemove(channelId, out _);
    }

    public ChatChannel? GetChannel(int channelId)
    {
        return _channels.TryGetValue(channelId, out var channel) ? channel : null;
    }

    public IEnumerable<ChatChannel> GetAllChannels()
    {
        return _channels.Values;
    }
}
