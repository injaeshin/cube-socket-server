using Microsoft.Extensions.Logging;

using Common.Network.Handler;
using Common.Network.Message;
using Common.Network.Packet;
using Common.Network.Session;

using Server.Chat.Service;

namespace Server.Chat.Handler;

public class ChatMessageHandler(ILogger<ChatMessageHandler> logger, IChatService chatService) : IPacketHandler
{
    private readonly ILogger _logger = logger;
    private readonly IChatService _chatService = chatService;

    public PacketType PacketType => PacketType.ChatMessage;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        try
        {
            // 원시 패킷 데이터 로깅
            _logger.LogDebug("Chat packet raw data: {HexDump}, Length: {Length}", 
                BitConverter.ToString(packet.ToArray()), packet.Length);
                
            var reader = new PacketReader(packet);
            var packetType = reader.ReadPacketType();
            if (packetType != PacketType.ChatMessage)
            {
                _logger.LogError("Invalid packet type: {PacketType}, expected: {ExpectedType}", packetType, PacketType.ChatMessage);
                return false;
            }

            // 남은 데이터 확인
            _logger.LogDebug("Remaining data after packet type: {RemainingLength} bytes", reader.RemainingLength);
            if (reader.RemainingLength < 2)
            {
                _logger.LogError("Chat message packet is too short, no sender length field");
                return false;
            }

            // 메시지 읽기 전에 위치 기록
            var position = reader.Position;
            
            try
            {
                var chatMessage = ChatMessage.Create(ref reader);
                _logger.LogInformation("ChatMessage: {Message} from {Sender}, Position after read: {Position}", 
                    chatMessage.Message, chatMessage.Sender, reader.Position);

                await _chatService.SendChatMessageToAll(chatMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing chat message at position {Position}", position);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat packet");
            return false;
        }
    }
}
