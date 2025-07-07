using Cube.Packet;
using Cube.Packet.Builder;
using Cube.Server.Chat.Model;

namespace Cube.Server.Chat.Service;

public interface IChatService
{
    Task SendToAll(ChatMessage message);
}

public class ChatService(IManagerContext managerContext) : IChatService
{
    private readonly IManagerContext _managerContext = managerContext;

    public async Task SendToAll(ChatMessage message)
    {
        var (data, rentedBuffer) = new PacketWriter(PacketType.ChatMessage)
                                        .WriteString(message.Sender)
                                        .WriteString(message.Message)
                                        .ToTcpPacket();
        await _managerContext.SessionManager.SendToAll(data, rentedBuffer);
        BufferArrayPool.Return(rentedBuffer);
    }
}
