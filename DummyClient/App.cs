using Common;
using Common.Protocol;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace __DummyClient;

public class App(ILogger<App> logger) : IDisposable
{
    private readonly ILogger<App> _logger = logger;
    private Socket? _socket;
    private bool _running = false;
    private readonly byte[] _receiveBuffer = new byte[Constants.PACKET_BUFFER_SIZE];
    private readonly PacketBuffer _packetBuffer = new PacketBuffer();
    private bool _disposed = false;  // 이미 Dispose 되었는지 추적

    public async Task RunAsync()
    {
        _logger.LogInformation("Dummy client started.");

        try
        {
            // 서버 연결
            if (!await ConnectToServerAsync("127.0.0.1", 7777))
                return;

            _running = true;

            // 로그인 요청
            await SendLoginRequestAsync("TestUser", "password123");

            // 비동기 패킷 수신 시작
            var receiveTask = StartReceivingPacketsAsync();

            // 사용자 입력 처리
            await HandleUserInputAsync();

            // 연결 종료 처리
            await DisconnectAsync();

            // 수신 태스크 완료 대기
            await receiveTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in RunAsync");
        }

        _logger.LogInformation("Press any key to exit...");
        Console.ReadKey();
    }

    private async Task<bool> ConnectToServerAsync(string host, int port)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _socket.ConnectAsync(host, port);
            _packetBuffer.Reset(); // 연결 시 버퍼 초기화
            _logger.LogInformation("Connected to server");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection error");
            return false;
        }
    }

    private async Task SendLoginRequestAsync(string username, string password)
    {
        if (_socket == null || !_socket.Connected)
            return;

        try
        {
            using var writer = new PacketWriter();
            writer.Write(username);
            writer.Write(password);
            var payload = writer.ToMemory();

            await SendPacketAsync(PacketType.Login, payload);
            _logger.LogInformation("Login packet sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
        }
    }

    private async Task SendPacketAsync(PacketType type, ReadOnlyMemory<byte> payload)
    {
        if (_socket == null || !_socket.Connected)
            return;

        var packet = PacketIO.Build("dummy-session", _socket, type, payload);
        try
        {
            await _socket.SendAsync(packet.Data, SocketFlags.None);
        }
        finally
        {
            packet.Return();
        }
    }

    private async Task SendPingAsync()
    {
        _logger.LogInformation("Sending manual ping...");
        await SendPacketAsync(PacketType.Ping, Array.Empty<byte>());
    }

    private async Task SendPongAsync()
    {
        await SendPacketAsync(PacketType.Pong, Array.Empty<byte>());
        _logger.LogInformation("Sent Pong response");
    }

    private Task StartReceivingPacketsAsync()
    {
        return Task.Run(async () =>
        {
            while (_running && _socket != null && _socket.Connected)
            {
                try
                {
                    int received = await _socket.ReceiveAsync(_receiveBuffer, SocketFlags.None);
                    if (received == 0)
                    {
                        _logger.LogInformation("[Server disconnected]");
                        _running = false;
                        break;
                    }

                    // 수신된 데이터를 패킷 버퍼에 추가
                    if (!_packetBuffer.Append(new ReadOnlyMemory<byte>(_receiveBuffer, 0, received)))
                    {
                        _logger.LogError("[Error appending to packet buffer]");
                        continue;
                    }

                    // 완전한 패킷이 버퍼에 있는 동안 계속 처리
                    while (_packetBuffer.TryReadPacket(out var packet, out var rentedBuffer))
                    {
                        try
                        {
                            await ProcessPacketAsync(packet);
                        }
                        finally
                        {
                            // 사용 후 임대된 버퍼 반환 (있는 경우)
                            if (rentedBuffer != null)
                            {
                                ArrayPool<byte>.Shared.Return(rentedBuffer);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Receive error");
                    _running = false;
                }
            }
        });
    }

    private async Task ProcessPacketAsync(ReadOnlyMemory<byte> packetData)
    {
        var packetType = PacketIO.GetPacketType(packetData);
        var packetPayload = PacketIO.GetPayload(packetData);

        switch (packetType)
        {
            case PacketType.Ping:
                _logger.LogInformation("Received Ping from server");
                await SendPongAsync();
                break;

            case PacketType.LoginSuccess:
                ProcessLoginResponse(packetPayload);
                break;

            default:
                _logger.LogInformation("Received packet: {packetType}, Size: {Length}", packetType, packetData.Length);
                break;
        }
    }

    private void ProcessLoginResponse(ReadOnlyMemory<byte> payload)
    {
        string message = Encoding.UTF8.GetString(payload.Span);
        _logger.LogInformation("[Login Response] {message}", message);
    }

    private async Task HandleUserInputAsync()
    {
        _logger.LogInformation("Client is running. Press 'Q' to quit, 'P' to send custom ping.");

        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.Q:
                        _logger.LogInformation("Quitting...");
                        _running = false;
                        break;
                    case ConsoleKey.P:
                        await SendPingAsync();
                        break;
                }
            }

            await Task.Delay(100);
        }
    }

    private Task DisconnectAsync()
    {
        if (_socket == null || !_socket.Connected)
            return Task.CompletedTask;

        try
        {
            _running = false;  // 먼저 수신 루프 중지
            
            // 안전하게 종료 시도
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _logger.LogInformation("[Disconnected]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Disconnect Error]");
            
            // Shutdown에 실패해도 Close는 시도
            try 
            {
                _socket.Close();
            }
            catch {}
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // 관리되는 리소스 정리
            _running = false;
            
            // 아직 연결이 있다면 연결 종료
            if (_socket != null)
            {
                try
                {
                    if (_socket.Connected)
                    {
                        try { _socket.Shutdown(SocketShutdown.Both); } catch {}
                        _socket.Close();
                    }
                    _socket.Dispose();
                    _logger.LogInformation("[Socket Disposed]");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Socket Dispose Error]");
                }
                _socket = null;
            }
        }

        _disposed = true;
    }

    ~App()
    {
        Dispose(false);
    }
}

