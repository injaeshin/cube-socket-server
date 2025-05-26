using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Cube.Core.Sessions;

using Cube.Server.Chat.Helper;
using Cube.Server.Chat.User;
using Cube.Server.Chat.Model;
using Cube.Server.Chat.Channel;

namespace Cube.Server.Chat.Service;

public interface IChatService
{
    Task<bool> AddUser(string userName, ISession session);
    Task<bool> DeleteUser(string sessionId);
    Task<bool> JoinChannel(int channelId, IChatUser user);
    Task<bool> LeaveChannel(int channelId, IChatUser user);

    bool TryGetUserBySession(string sessionId, out IChatUser? user);
    Task SendChatMessageToChannel(int channelId, ChatMessage message);
}

public class ChatService : IChatService
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<ChatService>();
    private readonly ConcurrentDictionary<int, ChatChannel> _channels = new();
    private readonly IUserManager _userManager;

    public ChatService(IUserManager userManager)
    {
        _userManager = userManager;
        _channels.TryAdd(1, new ChatChannel(1, "General"));
    }

    public async Task<bool> AddUser(string userName, ISession session)
    {
        _logger.LogDebug("Adding user: {UserName}, SessionId: {SessionId}", userName, session.SessionId);

        if (!_userManager.TryInsertUser(userName, session, out var user))
        {
            return false;
        }

        if (!await JoinChannel(1, user!))
        {
            _logger.LogError("Failed to join channel: {ChannelId}", 1);
            _userManager.DeleteUser(session.SessionId);
            return false;
        }

        return true;
    }

    public async Task<bool> DeleteUser(string sessionId)
    {
        var user = _userManager.GetUserBySession(sessionId);
        if (user == null)
        {
            _logger.LogError("Failed to delete user: {SessionId}", sessionId);
            return false;
        }

        if (!_userManager.DeleteUser(sessionId))
        {
            _logger.LogError("Failed to delete user: {SessionId}", sessionId);
            return false;
        }

        await LeaveChannel(1, user);

        return true;
    }

    public async Task<bool> JoinChannel(int channelId, IChatUser user)
    {
        if (!_channels.TryGetValue(channelId, out var channel))
        {
            return false;
        }

        return await channel.TryAddMember(user);
    }

    public async Task<bool> LeaveChannel(int channelId, IChatUser user)
    {
        if (!_channels.TryGetValue(channelId, out var channel))
        {
            return false;
        }

        return await channel.TryRemoveMember(user);
    }

    public bool TryGetUserBySession(string sessionId, out IChatUser? user)
    {
        user = _userManager.GetUserBySession(sessionId);
        return user != null;
    }

    public async Task SendChatMessageToChannel(int channelId, ChatMessage message)
    {
        if (_channels.TryGetValue(channelId, out var channel))
        {
            await channel.SendMessageToAll(message);
        }
    }
}
