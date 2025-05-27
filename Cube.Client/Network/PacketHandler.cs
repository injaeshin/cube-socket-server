using Cube.Packet;

namespace Cube.Client.Network;

public class PacketHandler
{
    private readonly TcpClient _client;
    public event Action<string>? OnError;
    public event Action<string, string>? OnChatMessageReceived;
    public event Action? OnLoginSuccess;
    public event Action? OnPingReceived;

    public PacketHandler(TcpClient client)
    {
        _client = client;
        _client.OnPacketReceived += HandlePacket;
        _client.OnError += (error) => OnError?.Invoke(error);
    }

    private void HandlePacket(PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        try
        {
            var reader = new PacketReader(payload);

            switch (packetType)
            {
                case PacketType.LoginSuccess:
                    OnLoginSuccess?.Invoke();
                    break;

                case PacketType.ChatMessage:
                    var sender = reader.ReadString();
                    var message = reader.ReadString();
                    OnChatMessageReceived?.Invoke(sender, message);
                    break;

                case PacketType.Ping:
                    OnPingReceived?.Invoke();
                    _ = SendPong();
                    break;

                case PacketType.Pong:
                    OnError?.Invoke($"Pong!?: {packetType}");
                    break;

                default:
                    OnError?.Invoke($"알 수 없는 패킷 타입: {packetType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"패킷 처리 오류: {ex.Message}");
        }
    }

    public async Task SendLoginAsync(string username)
    {
        try
        {
            var writer = new PacketWriter();
            writer.WriteType((ushort)PacketType.Login);
            writer.WriteString(username);
            await _client.SendAsync(writer);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"로그인 패킷 전송 실패: {ex.Message}");
        }
    }

    public async Task SendChatMessageAsync(string message)
    {
        try
        {
            var writer = new PacketWriter();
            writer.WriteType((ushort)PacketType.Chat);
            writer.WriteString(message);
            await _client.SendAsync(writer);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"채팅 패킷 전송 실패: {ex.Message}");
        }
    }

    private async Task SendPong()
    {
        try
        {
            var writer = new PacketWriter();
            writer.WriteType((ushort)PacketType.Pong);
            await _client.SendAsync(writer);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Pong 패킷 전송 실패: {ex.Message}");
        }
    }
}