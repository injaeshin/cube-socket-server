using Common.Network.Packet;
using Common.Network;
using Microsoft.Extensions.Logging;

namespace DummyClient;

public class MessageSender
{
    private readonly ClientSession _session;
    private readonly ILogger _logger;
    public MessageSender(ClientSession session, ILogger<MessageSender> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task SendLoginAsync(string username)
    {
        using var writer = new PacketWriter(MessageType.Login);
        writer.WriteString(username);
        await _session.SendAsync(writer.ToPacket());
    }

    public async Task SendChatMessageAsync(string message)
    {
        using var writer = new PacketWriter(MessageType.ChatMessage);
        writer.WriteString(message);
        await _session.SendAsync(writer.ToPacket());
    }

    public async Task SendLogoutAsync()
    {
        using var writer = new PacketWriter(MessageType.Logout);
        await _session.SendAsync(writer.ToPacket());
    }
}