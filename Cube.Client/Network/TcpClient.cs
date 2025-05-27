using System.Buffers;
using System.Net.Sockets;
using Cube.Common;
using Cube.Core;
using Cube.Core.Network;
using Cube.Packet;

namespace Cube.Client.Network;

public class TcpClient : IDisposable
{
    private readonly System.Net.Sockets.TcpClient _client;
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private NetworkStream? _stream;
    private bool _isConnected;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PacketBuffer _packetBuffer;

    public event Action<string>? OnError;
    public event Action? OnDisconnected;
    public event Action<PacketType, ReadOnlyMemory<byte>>? OnPacketReceived;

    public TcpClient(string serverAddress, int serverPort)
    {
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _client = new System.Net.Sockets.TcpClient();
        _cancellationTokenSource = new CancellationTokenSource();
        _packetBuffer = new PacketBuffer();
    }

    public async Task ConnectAsync()
    {
        try
        {
            _client.NoDelay = true;
            await _client.ConnectAsync(_serverAddress, _serverPort);
            if (_client.Connected)
            {
                _stream = _client.GetStream();
                _isConnected = true;
                _ = ReceiveLoopAsync(_cancellationTokenSource.Token);
            }
            else
            {
                OnError?.Invoke("연결 실패: 서버에 연결할 수 없습니다.");
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"연결 실패: {ex.Message}");
            throw;
        }
    }

    public async Task SendAsync(PacketWriter writer)
    {
        if (!_isConnected || _stream == null)
        {
            OnError?.Invoke("연결되지 않은 상태입니다.");
            return;
        }

        try
        {
            var (data, rentedBuffer) = writer.ToTcpPacket();
            await _stream.WriteAsync(data);
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"패킷 전송 실패: {ex.Message}");
            Disconnect();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[Consts.BUFFER_SIZE];
            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                if (_stream == null) break;

                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                if (!_packetBuffer.TryAppend(buffer.AsMemory(0, bytesRead)))
                {
                    OnError?.Invoke("버퍼 오버플로우");
                    break;
                }

                while (_packetBuffer.TryGetValidatePacket(out var packetType, out var payload, out var rentedBuffer))
                {
                    try
                    {
                        OnPacketReceived?.Invoke((PacketType)packetType, payload);
                    }
                    finally
                    {
                        PacketBuffer.Return(rentedBuffer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"수신 오류: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    public void Disconnect()
    {
        if (!_isConnected) return;

        _isConnected = false;
        _cancellationTokenSource.Cancel();
        _stream?.Close();
        _client.Close();
        OnDisconnected?.Invoke();
    }

    public void Dispose()
    {
        Disconnect();
        _cancellationTokenSource.Dispose();
    }
}