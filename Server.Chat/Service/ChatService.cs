using Common.Network.Message;
using Common.Network.Packet;
using Microsoft.Extensions.Logging;
using Server.Chat.Users;

namespace Server.Chat.Service;

public interface IChatService
{
    Task SendChatMessageToAll(ChatMessage message);
}

public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IUserManager _userManager;

    public ChatService(ILogger<ChatService> logger, IUserManager userManager)
    {
        _logger = logger;
        _userManager = userManager;
    }

    public async Task SendChatMessageToAll(ChatMessage message)
    {
        _logger.LogInformation("ChatMessageToAll: {message}", message);

        using var packet = ChatMessage.CreatePacket(message);
        foreach (var user in _userManager.GetAllUsers())
        {
            await user.Session.SendAsync(PacketType.ChatMessage, packet.ToMemory());
        }
    }
}

