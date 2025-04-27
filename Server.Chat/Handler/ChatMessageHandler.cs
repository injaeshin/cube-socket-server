using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Handler;
using Common.Network.Packet;
using Common.Network.Session;
using Common.Network.Message;
using Server.Chat.Helper;

namespace Server.Chat.Handler;

public class ChatMessageHandler() : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.Instance.CreateLogger<ChatMessageHandler>();

    public MessageType Type => MessageType.ChatMessage;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        try
        {
            // 원시 패킷 데이터 로깅
            _logger.LogDebug("Chat packet raw data: {HexDump}, Length: {Length}", 
                BitConverter.ToString(packet.ToArray()), packet.Length);

            try
            {
                var reader = new PacketReader(packet);
                var chatMessage = ChatMessage.Create(ref reader);
                _logger.LogInformation("ChatMessage: {Message} from {Sender}, Position after read: {Position}", 
                    chatMessage.Message, chatMessage.Sender, reader.Position);

                //await _chatService.SendChatMessageToAll(chatMessage);
                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing chat message at position");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat packet");
            return false;
        }

        return true;
    }
}
