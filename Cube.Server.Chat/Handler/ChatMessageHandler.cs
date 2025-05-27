using Microsoft.Extensions.Logging;

using Cube.Core;
using Cube.Core.Sessions;
using Cube.Common.Interface;
using Cube.Packet;
using Cube.Server.Chat.Service;
using Cube.Server.Chat.Helper;
using Cube.Server.Chat.Model;

namespace Cube.Server.Chat.Handler;

public class ChatMessageHandler(IChatService chatService) : PayloadPacketHandlerBase
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<ChatMessageHandler>();
    private readonly IChatService _chatService = chatService;

    public override PacketType Type => PacketType.ChatMessage;

    public override async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        try
        {
            // 원시 패킷 데이터 로깅
            _logger.LogDebug("Chat packet raw data: {HexDump}, Length: {Length}",
                BitConverter.ToString(packet.ToArray()), packet.Length);

            if (!_chatService.TryGetUserBySession(session.SessionId, out var user))
            {
                _logger.LogError("User not found for session: {SessionId}", session.SessionId);
                session.Close(new SessionClose(SocketDisconnect.NotFound));
                return false;
            }

            var reader = new PacketReader(packet);
            var chatMessage = new ChatMessage(user!.Name, reader.ReadString());
            // _logger.LogInformation("ChatMessage: {Message} from {Sender}, Position after read: {Position}", 
            //     chatMessage.Message, chatMessage.Sender, reader.Position);

            await _chatService.SendChatMessageToChannel(1, chatMessage);
            //await Task.Delay(10);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat packet");
            return false;
        }

        return true;
    }
}
