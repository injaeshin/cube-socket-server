using System.Collections.Concurrent;
using Cube.Common.Interface;
using Cube.Server.Chat.Model;
using Cube.Server.Chat.User;
using Microsoft.Extensions.Logging;

namespace Cube.Server.Chat.Channel;

public class ChatChannel(ILogger<ChatChannel> logger, IPacketFactory packetFactory)
{
    private readonly ILogger<ChatChannel> _logger = logger;
    private readonly ConcurrentDictionary<string, IChatUser> _members = new();
    private readonly IPacketFactory _packetFactory = packetFactory;

    private int _channelId = 0;
    private string _channelName = string.Empty;
    public int ChannelId => _channelId;
    public string ChannelName => _channelName;

    public int Count => _members.Count;

    public void SetChannelId(int channelId, string channelName)
    {
        _channelId = channelId;
        _channelName = channelName;
    }

    public async Task<bool> TryAddMember(IChatUser user)
    {
        if (!_members.TryAdd(user.Name, user))
        {
            return false;
        }

        var (payload, rentedBuffer) = _packetFactory.CreateChatMessagePacket(user.Name, $"{user.Name} 님이 채널에 참여했습니다.");

        foreach (var member in _members.Values)
        {
            if (member.Name == user.Name)
            {
                continue;
            }

            if (!member.Session.IsConnected)
            {
                continue;
            }

            await SendMessageAsync(member, payload, rentedBuffer);
        }

        return true;
    }

    public async Task<bool> TryRemoveMember(IChatUser user)
    {
        if (!_members.TryRemove(user.Name, out _))
        {
            return false;
        }

        var (payload, rentedBuffer) = _packetFactory.CreateChatMessagePacket(user.Name, $"{user.Name} 님이 채널에서 나갔습니다.");

        foreach (var member in _members.Values)
        {
            if (member.Name == user.Name)
            {
                continue;
            }

            if (!member.Session.IsConnected)
            {
                continue;
            }

            await SendMessageAsync(member, payload, rentedBuffer);
        }

        return true;
    }

    public IEnumerable<IChatUser> GetAllMembers()
    {
        return _members.Values;
    }

    public async Task SendMessageTo(string sender, string message, string target)
    {
        var (payload, rentedBuffer) = _packetFactory.CreateChatMessagePacket(sender, message);

        if (_members.TryGetValue(target, out var member))
        {
            if (!member.Session.IsConnected)
            {
                return;
            }

            await SendMessageAsync(member, payload, rentedBuffer);
        }
    }

    public async Task SendMessageToAll(ChatMessage message)
    {
        var (payload, rentedBuffer) = _packetFactory.CreateChatMessagePacket(message.Sender, message.Message);

        foreach (var member in _members.Values)
        {
            if (!member.Session.IsConnected)
            {
                continue;
            }

            await SendMessageAsync(member, payload, rentedBuffer);
        }
    }

    private async Task SendMessageAsync(IChatUser target, Memory<byte> payload, byte[]? rentedBuffer)
    {
        await target.Session.SendAsync(payload, rentedBuffer);
    }
}
