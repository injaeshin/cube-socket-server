using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Handler;
using Common.Network.Packet;
using Common.Network.Session;
using Common.Network.Message;
using Server.Chat.Helper;
using Server.Chat.Service;

namespace Server.Chat.Handler;

public class ChatMessageHandler(IChatService chatService) : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<ChatMessageHandler>();
    private readonly IChatService _chatService = chatService;

    public MessageType Type => MessageType.ChatMessage;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        try
        {
            // 원시 패킷 데이터 로깅
            _logger.LogDebug("Chat packet raw data: {HexDump}, Length: {Length}", 
                BitConverter.ToString(packet.ToArray()), packet.Length);

            if (!_chatService.TryGetUserBySession(session.SessionId, out var user))
            {
                _logger.LogError("User not found for session: {SessionId}", session.SessionId);
                session.Close(DisconnectReason.ApplicationRequest);
                return false;
            }

            var reader = new PacketReader(packet);
            var chatMessage = new ChatMessage(user!.Name, reader.ReadString());
            _logger.LogInformation("ChatMessage: {Message} from {Sender}, Position after read: {Position}", 
                chatMessage.Message, chatMessage.Sender, reader.Position);

            await _chatService.SendChatMessageToChannel(1, chatMessage);
            await Task.Delay(10);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat packet");
            return false;
        }

        return true;
    }
}
