using Microsoft.Extensions.Logging;
using Common.Network.Packet;
using Common.Network;

namespace __DummyClient;

public partial class DummyClient
{
    private async Task SendChatMessageAsync(string message)
    {
        if (_session == null || !_session.IsConnected)
            return;

        try
        {
            using var writer = new PacketWriter();
            writer.WriteType(MessageType.ChatMessage).WriteString(message);
            await _session.SendAsync(writer.ToPacket());
            _logger.LogInformation("[{name}] Sent ChatMessage: {message}", _name, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] 채팅 메시지 전송 오류", _name);
        }
    }
}