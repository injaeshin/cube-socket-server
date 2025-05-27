using Cube.Client.Network;

namespace Cube.Client;

public class ChatClient : IDisposable
{
    private readonly TcpClient _client;
    private readonly PacketHandler _packetHandler;
    private bool _isRunning;

    public event Action<string>? OnError;
    public event Action<string>? OnStatusChanged;
    public event Action<string, string>? OnChatMessageReceived;

    public ChatClient(string serverAddress, int serverPort)
    {
        _client = new TcpClient(serverAddress, serverPort);
        _packetHandler = new PacketHandler(_client);

        _client.OnError += (error) => OnError?.Invoke(error);
        _client.OnDisconnected += () =>
        {
            OnStatusChanged?.Invoke("서버와의 연결이 종료되었습니다.");
            _isRunning = false;
        };

        _packetHandler.OnError += (error) => OnError?.Invoke(error);
        _packetHandler.OnPingReceived += () => OnStatusChanged?.Invoke("Ping!");
        _packetHandler.OnLoginSuccess += () => OnStatusChanged?.Invoke("로그인 성공!");
        _packetHandler.OnChatMessageReceived += (sender, message) =>
            OnChatMessageReceived?.Invoke(sender, message);
    }

    public async Task StartAsync()
    {
        try
        {
            await _client.ConnectAsync();
            OnStatusChanged?.Invoke("서버에 연결되었습니다.");
            _isRunning = true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"연결 실패: {ex.Message}");
            throw;
        }
    }

    public async Task LoginAsync(string username)
    {
        await _packetHandler.SendLoginAsync(username);
    }

    public async Task SendChatMessageAsync(string message)
    {
        if (!_isRunning)
        {
            OnError?.Invoke("연결되지 않은 상태입니다.");
            return;
        }

        await _packetHandler.SendChatMessageAsync(message);
    }

    public void Stop()
    {
        _isRunning = false;
        _client.Disconnect();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}