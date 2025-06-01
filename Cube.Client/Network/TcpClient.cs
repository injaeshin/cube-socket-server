using System.Net;
using Cube.Packet;

namespace Cube.Client.Network;

public class TcpClient : IDisposable
{
    private readonly TcpSocket _socket;
    private bool _disposed;
    private bool _isConnected;

    public string ServerAddress { get; }
    public int ServerPort { get; }

    public event Action<string>? OnStatusChanged;
    public event Action<PacketType, ReadOnlyMemory<byte>>? OnPacketReceived;

    public TcpClient(string serverAddress, int serverPort)
    {
        ServerAddress = serverAddress;
        ServerPort = serverPort;
        _socket = new TcpSocket();
        _socket.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
        _socket.OnDataReceived += OnSocketDataReceived;
    }

    private void OnSocketDataReceived(PacketType packetType, ReadOnlyMemory<byte> data)
    {
        if (!_isConnected) return;

        OnPacketReceived?.Invoke(packetType, data);
    }

    public void Connect()
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("이미 연결된 상태입니다.");
        }

        try
        {
            _socket.Connect(ServerAddress, ServerPort);
            _isConnected = true;
            OnStatusChanged?.Invoke("TCP 연결 성공!");
        }
        catch (Exception ex)
        {
            throw new Exception($"TCP 연결 실패: {ex.Message}");
        }
    }

    public void Send(Memory<byte> data)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("연결되지 않은 상태입니다.");
        }

        try
        {
            _socket.Send(data);
        }
        catch (Exception ex)
        {
            throw new Exception($"TCP 메시지 전송 실패: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        if (_isConnected)
        {
            _socket.Disconnect();
            _isConnected = false;
        }
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
            Disconnect();
            _socket.Dispose();
        }
    }
}