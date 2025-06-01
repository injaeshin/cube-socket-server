using Cube.Client.Network;
using Cube.Packet;

namespace Cube.Client;

public class ChatClient : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly UdpClient _udpClient;
    private readonly PacketHandler _packetHandler;
    private bool _isRunning;
    private string? _sessionToken;

    public event Action<string>? OnError;
    public event Action<string>? OnStatusChanged;
    public event Action<string, string>? OnChatMessageReceived;

    private readonly string _username = $"Player{Random.Shared.Next(100000000, 999999999)}";

    public ChatClient(string ip, int tcpPort, int udpPort)
    {
        _tcpClient = new TcpClient(ip, tcpPort);
        _udpClient = new UdpClient(ip, udpPort);
        _packetHandler = new PacketHandler(_tcpClient, _udpClient);

        _packetHandler.OnChatMessageReceived += (sender, message) => OnChatMessageReceived?.Invoke(sender, message);
        _packetHandler.OnLoginSuccess += (token) =>
        {
            _sessionToken = token;
            OnStatusChanged?.Invoke($"로그인 성공! (세션 토큰: {token})");
            ConnectUdp();
        };
        _packetHandler.OnPingReceived += () => OnStatusChanged?.Invoke("Ping!");
        _packetHandler.OnWelcomeReceived += (message) => OnStatusChanged?.Invoke(message);
    }

    public void Connect()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("이미 연결된 상태입니다.");
        }

        try
        {
            _isRunning = true;
            _tcpClient.Connect();
            OnStatusChanged?.Invoke("TCP 서버에 연결되었습니다.");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"연결 실패: {ex.Message}");
            throw;
        }
    }

    private void ConnectUdp()
    {
        if (string.IsNullOrEmpty(_sessionToken))
        {
            throw new Exception("세션 토큰이 없습니다.");
        }

        try
        {
            _udpClient.Connect(_sessionToken);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"UDP 연결 실패: {ex.Message}");
            throw;
        }
    }

    public void Login()
    {
        _packetHandler.ReceiveLoop();
        _packetHandler.SendLogin(_username);
    }

    public void SendChatMessage(string message)
    {
        if (!_isRunning)
        {
            throw new Exception("연결되지 않은 상태입니다.");
        }

        try
        {
            _packetHandler.SendChatMessage(message);
        }
        catch (Exception ex)
        {
            throw new Exception($"메시지 전송 실패: {ex.Message}");
        }
    }

    public void SendUdpMessage(string message)
    {
        if (!_isRunning || string.IsNullOrEmpty(_sessionToken))
        {
            throw new Exception("연결되지 않은 상태입니다.");
        }

        try
        {
            var (data, rentedBuffer) = new PacketWriter(PacketType.ChatMessage)
                                            .WriteString(message)
                                            .ToUdpPacket();
            _udpClient.Send(data, rentedBuffer);
        }
        catch (Exception ex)
        {
            throw new Exception($"UDP 메시지 전송 실패: {ex.Message}");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _tcpClient.Disconnect();
        _udpClient.Close();
        _packetHandler.Stop();
    }

    public void Dispose()
    {
        _tcpClient.Dispose();
        _udpClient.Dispose();
        _packetHandler.Dispose();
    }
}