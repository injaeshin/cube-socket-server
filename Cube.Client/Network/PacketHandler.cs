using Cube.Packet;

namespace Cube.Client.Network;

public class PacketHandler : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly UdpClient _udpClient;
    private bool _isRunning;
    private bool _disposed;

    public event Action<string, string>? OnChatMessageReceived;
    public event Action<string>? OnLoginSuccess;
    public event Action? OnPingReceived;
    public event Action<string>? OnWelcomeReceived;

    public PacketHandler(TcpClient tcpClient, UdpClient udpClient)
    {
        _tcpClient = tcpClient;
        _udpClient = udpClient;

        _tcpClient.OnPacketReceived += OnPacketReceived;
        _udpClient.OnPacketReceived += OnPacketReceived;
    }

    private void OnPacketReceived(PacketType packetType, ReadOnlyMemory<byte> data)
    {
        if (!_isRunning) return;

        switch (packetType)
        {
            case PacketType.LoginSuccess:
                var reader = new PacketReader(data);
                var token = reader.ReadString();
                OnLoginSuccess?.Invoke(token);
                break;
            case PacketType.Welcome:
                //reader = new PacketReader(data);
                //var welcomeMessage = reader.ReadString();
                OnWelcomeReceived?.Invoke("UDP 연결 성공");
                break;

            case PacketType.ChatMessage:
                reader = new PacketReader(data);
                var sender = reader.ReadString();
                var message = reader.ReadString();
                OnChatMessageReceived?.Invoke(sender, message);
                break;

            case PacketType.Ping:
                OnPingReceived?.Invoke();
                break;
        }
    }

    public void ReceiveLoop()
    {
        _isRunning = true;
    }

    public void SendLogin(string username)
    {
        using var writer = new PacketWriter(PacketType.Login);
        writer.WriteString(username);
        var (data, _) = writer.ToTcpPacket();
        _tcpClient.Send(data);
    }

    public void SendChatMessage(string message)
    {
        using var writer = new PacketWriter(PacketType.ChatMessage);
        writer.WriteString(message);
        var (data, _) = writer.ToTcpPacket();
        _tcpClient.Send(data);
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            Stop();
        }
    }
}