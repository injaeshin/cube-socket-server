using Microsoft.Extensions.Logging;

using System.Buffers;
using System.Net.Sockets;
using System.Text;

using Common.Network;
using Common.Network.Packet;
using Common.Network.Message;

namespace __DummyClient;

public class App(ILogger<App> logger) : IDisposable
{
    private readonly ILogger<App> _logger = logger;
    private Socket? _socket;
    private bool _running = false;
    private readonly byte[] _receiveBuffer = new byte[Constant.BUFFER_SIZE];
    private readonly PacketBuffer _packetBuffer = new();
    private bool _disposed = false;  // 이미 Dispose 되었는지 추적

    private string _name = string.Empty;

    public async Task RunAsync()
    {
        _logger.LogInformation("Dummy client started.");

        try
        {
            // 서버 연결
            if (!await ConnectToServerAsync("127.0.0.1", 7777))
                return;

            _running = true;

            _name = CreateRandomName();

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

    private static string CreateRandomMessage()
    {
        return $"message_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    private static string CreateRandomName()
    {
        // 임의 숫자 (2자리)
        var idx = Random.Shared.Next(10, 99);
        return $"user_{idx}";
    }

    private async Task<bool> ConnectToServerAsync(string host, int port)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 네이글 알고리즘 비활성화 (작은 패킷을 즉시 전송, 지연 없음)
            _socket.NoDelay = true;
            await _socket.ConnectAsync(host, port);
            _packetBuffer.Reset(); // 연결 시 버퍼 초기화
            _logger.LogInformation("[{name}] Connected to server", _name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Connection error", _name);
            return false;
        }
    }

    private async Task SendLoginRequestAsync(string username)
    {
        if (_socket == null || !_socket.Connected)
            return;

        try
        {
            // 사용자명의 길이가 너무 길지 않도록 제한
            if (username.Length > 20)
            {
                username = username.Substring(0, 20);
            }
            
            // 패킷 페이로드를 직접 구성하여 문자열이 올바르게 인코딩되도록 함
            using var writer = new PacketWriter();
            writer.Write(username);  // 문자열 자체를 바로 쓰기
            
            var payload = writer.ToMemory();
            
            _logger.LogDebug("[{name}] 로그인 요청 페이로드: 길이={length}, 내용={hex}", 
                _name, payload.Length, BitConverter.ToString(payload.ToArray()));
                
            await SendPacketAsync(PacketType.Login, payload);
            _logger.LogInformation("[{name}] Login request sent with username: {username}", _name, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Login error", _name);
        }
    }

    private async Task SendPacketAsync(PacketType type, ReadOnlyMemory<byte> payload)
    {
        if (_socket == null || !_socket.Connected)
            return;

        var packet = PacketIO.Build(_name, _socket, type, payload);
        try
        {
            // 패킷 내용 디버깅 로그 추가
            _logger.LogDebug("[{name}] Sending packet: Type={type}({typeValue:X4}), PayloadLength={payloadLength}, TotalLength={totalLength}",
                _name, type, (ushort)type, payload.Length, packet.Data.Length);
            
            if (packet.Data.Length > 0)
            {
                // 패킷 헤더 바이트 확인
                byte[] headerBytes = packet.Data.Span.Slice(0, Math.Min(4, packet.Data.Length)).ToArray();
                _logger.LogDebug("[{name}] Packet header bytes: {headerHex}",
                    _name, BitConverter.ToString(headerBytes));
            }
            
            await _socket.SendAsync(packet.Data, SocketFlags.None);
            _logger.LogInformation("[{name}] Packet sent: Type={type}, Length={length}", _name, type, packet.Data.Length);
        }
        finally
        {
            packet.Return();
        }
    }

    private async Task SendPingAsync()
    {
        _logger.LogInformation("[{name}] Sending manual ping...", _name);
        await SendPacketAsync(PacketType.Ping, Array.Empty<byte>());
    }

    private async Task SendPongAsync()
    {
        await SendPacketAsync(PacketType.Pong, Array.Empty<byte>());
        _logger.LogInformation("[{name}] Sent Pong response", _name);
    }

    private async Task SendChatMessageAsync(string message)
    {
        try
        {
            // 메시지 길이 제한
            if (message.Length > 200)
            {
                message = message.Substring(0, 200); 
            }
            
            // 패킷 페이로드를 직접 구성하여 문자열이 올바르게 인코딩되도록 함
            using var writer = new PacketWriter();
            writer.Write(message);  // 문자열 인코딩 (길이 포함)
            
            var payload = writer.ToMemory();
            
            _logger.LogDebug("[{name}] 채팅 메시지 페이로드: 길이={length}, 내용={hex}", 
                _name, payload.Length, BitConverter.ToString(payload.ToArray()));
            
            await SendPacketAsync(PacketType.ChatMessage, payload);
            _logger.LogInformation("[{name}] Sent ChatMessage: {message}", _name, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] 채팅 메시지 전송 오류", _name);
        }
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
                        _logger.LogInformation("[{name}] Server disconnected", _name);
                        _running = false;
                        break;
                    }

                    // 수신 데이터 디버깅 (처음 몇 바이트만)
                    int bytesToLog = Math.Min(received, 16);
                    byte[] receivedBytes = new byte[bytesToLog];
                    Array.Copy(_receiveBuffer, receivedBytes, bytesToLog);
                    _logger.LogDebug("[{name}] Received {received} bytes: {hexDump}{more}",
                        _name, received, BitConverter.ToString(receivedBytes),
                        received > bytesToLog ? "..." : "");

                    // 수신된 데이터를 패킷 버퍼에 추가
                    if (!_packetBuffer.TryAppend(new ReadOnlyMemory<byte>(_receiveBuffer, 0, received)))
                    {
                        _logger.LogError("[{name}] Error appending to packet buffer", _name);
                        continue;
                    }

                    // 완전한 패킷이 버퍼에 있는 동안 계속 처리
                    while (_packetBuffer.TryReadPacket(out var packet, out var rentedBuffer))
                    {
                        try
                        {
                            // 패킷 데이터 디버깅 (처음 몇 바이트만)
                            if (packet.Length > 0)
                            {
                                int packetBytesToLog = Math.Min(packet.Length, 16);
                                byte[] packetBytes = packet.Slice(0, packetBytesToLog).ToArray();
                                _logger.LogDebug("[{name}] Extracted packet ({length} bytes): {hexDump}{more}",
                                    _name, packet.Length, BitConverter.ToString(packetBytes),
                                    packet.Length > packetBytesToLog ? "..." : "");
                            }
                            
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
                    _logger.LogError(ex, "[{name}] Receive error", _name);
                    _running = false;
                }
            }
        });
    }

    private async Task ProcessPacketAsync(ReadOnlyMemory<byte> packetData)
    {
        try
        {
            if (packetData.Length < Constant.OPCODE_SIZE)
            {
                _logger.LogError("[{name}] 패킷 크기가 너무 작습니다: {Length} 바이트", _name, packetData.Length);
                return;
            }

            // 바이트 레벨 디버깅
            var typeBytes = packetData.Slice(0, Constant.OPCODE_SIZE);
            ushort rawOpcode = (ushort)(typeBytes.Span[0] << 8 | typeBytes.Span[1]);

            // 패킷 타입 확인
            var packetType = PacketIO.GetPacketType(packetData);
            var packetPayload = PacketIO.GetPayload(packetData);
            
            // 디버깅을 위한 추가 로그
            int packetTypeValue = (int)packetType;
            _logger.LogDebug("[{name}] Processing packet: Type={packetType}({packetTypeValue:X4}), RawBytes=[{typeHex}], Size={Length}, Payload={PayloadSize}", 
                _name, packetType, packetTypeValue, BitConverter.ToString(typeBytes.ToArray()), packetData.Length, packetPayload.Length);

            // 패킷 타입 유효성 검증
            if (!Enum.IsDefined(typeof(PacketType), packetType) || packetTypeValue == 0 || packetTypeValue >= (int)PacketType.Max)
            {
                _logger.LogWarning("[{name}] 알 수 없는 패킷 타입: {rawOpcode:X4}, 바이트: {b0:X2} {b1:X2}", 
                    _name, rawOpcode, typeBytes.Span[0], typeBytes.Span[1]);
                
                // 전체 패킷 덤프 로깅
                _logger.LogDebug("[{name}] 패킷 덤프: {hexDump}", _name, BitConverter.ToString(packetData.ToArray()));
                return;
            }

            switch (packetType)
            {
                case PacketType.Ping:
                    _logger.LogInformation("[{name}] Received Ping from server", _name);
                    await SendPongAsync();
                    break;

                case PacketType.LoginSuccess:
                    ProcessLoginResponse(packetPayload);
                    break;

                case PacketType.ChatMessage:
                    ProcessChatMessage(packetPayload);
                    break;

                default:
                    _logger.LogWarning("[{name}] 처리되지 않은 패킷 타입: {packetType}({packetTypeValue:X4}), Size={Length}", 
                        _name, packetType, packetTypeValue, packetData.Length);
                    break;
            }
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "[{name}] 패킷 처리 중 오류 발생. 패킷 크기: {Length}", _name, packetData.Length);
            // 오류가 발생한 패킷 덤프
            if (packetData.Length > 0)
            {
                _logger.LogDebug("[{name}] 오류 패킷 덤프: {hexDump}", _name, BitConverter.ToString(packetData.ToArray()));
            }
        }
    }

    private void ProcessChatMessage(ReadOnlyMemory<byte> packetPayload)
    {
        var reader = new PacketReader(packetPayload);
        var message = ChatMessage.Create(ref reader);
        _logger.LogInformation("[{name}] Received ChatMessage: {Message} from {Sender}", _name, message.Message, message.Sender);
    }

    private void ProcessLoginResponse(ReadOnlyMemory<byte> payload)
    {
        string message = Encoding.UTF8.GetString(payload.Span);
        _logger.LogInformation("[{name}] Login Response: {message}", _name, message);
    }

    private async Task HandleUserInputAsync()
    {
        _logger.LogInformation("[{name}] Client is running. Press 'Q' to quit, 'P' to send custom ping.", _name);

        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.Q:
                        _logger.LogInformation("[{name}] Quitting...", _name);
                        _running = false;
                        break;
                    case ConsoleKey.W:
                        await SendLoginRequestAsync(_name);
                        break;
                    case ConsoleKey.E:
                        await SendChatMessageAsync(CreateRandomMessage());
                        break;
                    case ConsoleKey.R:
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
            _logger.LogInformation("[{name}] Disconnected", _name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Disconnect Error", _name);

            // Shutdown에 실패해도 Close는 시도
            try
            {
                _socket.Close();
            }
            catch { }
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
                        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                        _socket.Close();
                    }
                    _socket.Dispose();
                    _logger.LogInformation("[{name}] Socket Disposed", _name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{name}] Socket Dispose Error", _name);
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

