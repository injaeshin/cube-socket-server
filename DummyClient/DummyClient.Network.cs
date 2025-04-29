using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Common.Network.Buffer;
using Common.Network.Packet;
using Common.Network;
using Common.Network.Message;

namespace __DummyClient;

public partial class DummyClient
{
    private async Task<bool> ConnectToServerAsync()
    {
        try
        {
            await _session.ConnectAsync(_host, _port);
            _logger.LogInformation("[{name}] Connected to server", _name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Connection error", _name);
            return false;
        }
    }

    private async Task SendPacketAsync(ReadOnlyMemory<byte> packet)
    {
        try
        {
            await _session.SendAsync(packet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Packet send error", _name);
        }
    }

    private async Task OnReceivedPacketAsync(ReceivedPacket packet)
    {
        try
        {
            switch (packet.Type)
            {
                case MessageType.LoginSuccess:
                    _logger.LogInformation("[{name}] Login Success", _name);
                    break;
                case MessageType.ChatMessage:
                    var reader = new PacketReader(packet.Data);
                    var message = new ChatMessage(reader.ReadString(), reader.ReadString());
                    _logger.LogInformation("[{name}] Received ChatMessage: {Message} from {Sender}", _name, message.Message, message.Sender);
                    break;
                default:
                    _logger.LogDebug("[{name}] Unknown packet type: {packetType}", _name, packet.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] 패킷 처리 중 오류", _name);
        }

        await Task.CompletedTask;
    }
}