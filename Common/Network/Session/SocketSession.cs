using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Transport;

namespace Common.Network.Session;

public class SocketSession(SocketSessionOptions options, ILogger<SocketSession> logger) : ISession, IDisposable
{
    private readonly ILogger _logger = logger;
    public string SessionId { get; private set; } = null!;

    private Socket _socket = null!;
    private SocketAsyncEventArgs _receiveArgs = null!;
    private PacketBuffer _packetBuffer = new();

    private readonly SessionResource _resource = options.Resource;
    private readonly SessionQueue _queue = options.Queue;

    private static int _sessionCounter = 0; // 세션 카운터 (스레드 안전하게 관리해야 함)
    private static readonly object _counterLock = new object(); // 스레드 동기화를 위한 락 객체

    public event EventHandler<SessionEventArgs>? SessionConnected;
    public event EventHandler<SessionEventArgs>? SessionDisconnected;
    public event EventHandler<SessionDataEventArgs>? SessionDataReceived;

    public void CreateSessionId()
    {
        // 1. 타임스탬프 생성 (밀리초 단위)
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x"); // 16진수로 변환
        
        // 2. 증가하는 세션 번호 (스레드 안전하게)
        int sessionNumber;
        lock (_counterLock)
        {
            sessionNumber = ++_sessionCounter;
            if (_sessionCounter > 9999) _sessionCounter = 0; // 번호 재사용
        }
        
        // 3. 랜덤 값 추가 (충돌 방지)
        string randomPart = new Random().Next(0, 0xFFF).ToString("x3"); // 3자리 16진수
        
        // 세션 ID 형식: "SID_{timestamp}_{sessionNumber:D4}_{randomPart}"
        // 예: "SID_1a2b3c4d_0042_f7e"
        SessionId = $"SID_{timestamp}_{sessionNumber:D4}_{randomPart}";
    }

    public void Run(Socket socket)
    {
        _packetBuffer.Reset();

        ConfigureKeepAlive(socket);

        _socket = socket;
        _receiveArgs = _resource.OnRentRecvArgs.Invoke() ?? throw new Exception("Receive args is null");;
        _receiveArgs.Completed += OnReceiveCompleted;
        _receiveArgs.UserToken = this;

        SessionConnected?.Invoke(this, new SessionEventArgs(this));
        OnConnect(this);

        DoReceive();
    }

    /// <summary>
    /// 소켓의 KeepAlive 설정을 구성합니다.
    /// </summary>
    private void ConfigureKeepAlive(Socket socket)
    {
        // 모든 플랫폼에서 공통으로 적용되는 기본 KeepAlive 설정
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        try
        {
            // 모든 플랫폼에서 공통으로 사용할 KeepAlive 값 설정
            int keepAliveTime = 15;      // 유휴 시간(초) - 30초
            int keepAliveInterval = 5;   // 간격(초) - 1초
            int keepAliveRetryCount = 3; // 재시도 횟수 - 5회

            if (OperatingSystem.IsWindows())
            {
                // Windows에서는 IOControl 사용
                byte[] keepAliveValues = new byte[12];
                BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0);                      // 활성화(1)
                BitConverter.GetBytes(keepAliveTime * 1000).CopyTo(keepAliveValues, 4);   // 유휴 시간(ms)
                BitConverter.GetBytes(keepAliveInterval * 1000).CopyTo(keepAliveValues, 8); // 간격(ms)
                socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Linux/macOS에서는 소켓 옵션 사용
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTime);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveInterval);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, keepAliveRetryCount);
            }
        }
        catch (Exception ex)
        {
            // 예외 발생 시 기본 KeepAlive는 유지
            _logger.LogWarning("KeepAlive 세부 설정 적용 중 오류 발생: {Message}", ex.Message);
        }
    }

    private void DoReceive()
    {
        if (_socket.Connected == false)
            return;

        if (!_socket.ReceiveAsync(_receiveArgs))
        {
            OnReceiveCompleted(null, _receiveArgs);
        }
    }

    private static DisconnectReason GetDisconnectReason(SocketAsyncEventArgs e)
    {
        // 소켓 작업이 Receive가 아닌 경우
        if (e.LastOperation != SocketAsyncOperation.Receive)
        {
            return DisconnectReason.SocketError;
        }
        // 소켓 오류가 발생한 경우
        else if (e.SocketError != SocketError.Success)
        {
            return DisconnectReason.SocketError;
        }
        // 정상적인 연결 종료 감지 (0바이트 수신)
        else if (e.BytesTransferred == 0)
        {
            //Console.WriteLine($"[Session Closed] {SessionId} - Graceful disconnect detected (0 bytes received)");
            return DisconnectReason.GracefulDisconnect;
        }

        // 연결 종료 아님
        return DisconnectReason.None;
    }

    private async void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        try
        {
            // 연결 종료 확인
            DisconnectReason disconnectReason = GetDisconnectReason(e);
            if (disconnectReason != DisconnectReason.None)
            {
                ForceClose(disconnectReason);
                return;
            }

            var data = e.MemoryBuffer[..e.BytesTransferred];
            if (!_packetBuffer.Append(data))
            {
                _logger.LogWarning("Failed to append packet data");
                Close(DisconnectReason.InvalidData);
                return;
            }

            while (_packetBuffer.TryReadPacket(out var packet, out var rentedBuffer))
            {
                var packetType = PacketIO.GetPacketType(packet);
                SessionDataReceived?.Invoke(this, new SessionDataEventArgs(this, packetType, packet));
                await _queue.OnRecvEnqueueAsync(new ReceivedPacket(SessionId, packet, rentedBuffer, this));
            }

            DoReceive();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error onReceiveCompleted");
            Close(DisconnectReason.SocketError);
        }
    }

    public async Task SendAsync(PacketType type, ReadOnlyMemory<byte> payload)
    {
        var request = PacketIO.Build(SessionId, _socket, type, payload);
        _logger.LogDebug("[Session Send] {SessionId} {PacketType} {PayloadLength}", SessionId, type, payload.Length);

        await _queue.OnSendEnqueueAsync(request);
    }

    /// <summary>
    /// 세션을 종료합니다. 소켓 정리, 이벤트 발생, 리소스 반환을 수행합니다.
    /// </summary>
    /// <param name="reason">종료 이유</param>
    /// <param name="graceful">정상 종료 여부 (기본값: 종료 이유에 따라 자동 결정)</param>
    public void Close(DisconnectReason reason = DisconnectReason.ApplicationRequest)
    {
        // 이미 닫힌 소켓인지 확인 (중복 호출 방지)
        if (_socket == null || !_socket.Connected)
        {
            return;
        }

        try
        {
            // 종료 사유 로깅
            LogDisconnection(reason);
            
            // 소켓 정리, 이벤트 발생, 리소스 정리
            CleanupSession(IsGracefulReason(reason), reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session close: {Reason}", reason);
            SafeCleanupResources();
        }
    }

    /// <summary>
    /// 세션을 강제로 종료합니다. Shutdown을 수행하지 않고 바로 Close합니다.
    /// </summary>
    public void ForceClose(DisconnectReason reason = DisconnectReason.SocketError)
    {
        try
        {
            // 종료 사유 로깅
            LogDisconnection(reason);
            
            // 소켓 정리, 이벤트 발생, 리소스 정리
            CleanupSession(false, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session close: {Reason}", reason);
            SafeCleanupResources();
        }
    }

    /// <summary>
    /// 종료 이유가 정상 종료에 해당하는지 확인합니다.
    /// </summary>
    private static bool IsGracefulReason(DisconnectReason reason)
    {
        return  reason == DisconnectReason.GracefulDisconnect ||
                reason == DisconnectReason.ApplicationRequest ||
                reason == DisconnectReason.ServerShutdown;
    }

    /// <summary>
    /// 세션을 정리합니다. 소켓 정리, 이벤트 발생, 리소스 정리를 수행합니다.
    /// </summary>
    private void CleanupSession(bool graceful, DisconnectReason reason)
    {
        CleanupSocket(graceful);
        RaiseDisconnectEvents(reason);
        CleanupResources();
    }

    /// <summary>
    /// 소켓 연결을 정리합니다.
    /// </summary>
    private void CleanupSocket(bool graceful)
    {
        try
        {
            if (graceful && _socket != null && _socket.Connected)
            {
                // 양방향 통신 종료 (CLOSE_WAIT 상태 방지)
                _socket.Shutdown(SocketShutdown.Both);
            }

            _socket?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Socket cleanup error");
        }
    }

    /// <summary>
    /// 연결 종료 이벤트를 발생시킵니다.
    /// </summary>
    private void RaiseDisconnectEvents(DisconnectReason reason)
    {
        try
        {
            OnDisconnect(this, reason);
            SessionDisconnected?.Invoke(this, new SessionEventArgs(this));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising disconnect events");
        }
    }

    /// <summary>
    /// 이벤트 핸들러와 세션 리소스를 정리합니다.
    /// </summary>
    private void CleanupResources()
    {
        if (_receiveArgs != null)
        {
            _receiveArgs.Completed -= OnReceiveCompleted;
            _receiveArgs.UserToken = null;
            _resource.OnReturnRecvArgs?.Invoke(_receiveArgs);
        }

        // 세션 풀로 반환
        _resource.OnReturnSession?.Invoke(this);
    }
    
    /// <summary>
    /// 예외 발생 시에도 안전하게 리소스를 정리합니다.
    /// </summary>
    private void SafeCleanupResources()
    {
        try
        {
            CleanupResources();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return resources during error handling");
        }
    }

    // 연결 종료 이유에 따른 로깅
    private void LogDisconnection(DisconnectReason reason)
    {
        string reasonText = reason switch
        {
            DisconnectReason.GracefulDisconnect => "Client gracefully disconnected",
            DisconnectReason.SocketError => "Socket error occurred",
            DisconnectReason.ApplicationRequest => "Application requested disconnect",
            DisconnectReason.InvalidData => "Invalid data received",
            DisconnectReason.Timeout => "Connection timed out",
            DisconnectReason.ServerShutdown => "Server is shutting down",
            DisconnectReason.None => "No disconnect reason specified",
            _ => "Unhandled disconnect reason"
        };

        _logger.LogDebug("[Session Closed] {SessionId} - Reason: {ReasonText}", SessionId, reasonText);
        if (_socket?.RemoteEndPoint != null)
        {
            _logger.LogDebug("Remote endpoint: {RemoteEndPoint}", _socket.RemoteEndPoint);
        }
    }

    protected virtual void OnConnect(ISession session)
    {
        _logger.LogDebug("[Session Connected] {SessionId} {RemoteEndPoint}", SessionId, _socket.RemoteEndPoint);
    }

    protected virtual void OnDisconnect(ISession session, DisconnectReason reason)
    {
        _logger.LogDebug("[Session Disconnected] {SessionId} - Reason: {Reason}", SessionId, reason);
    }

    public virtual Task OnProcessReceivedAsync(ReadOnlyMemory<byte> data)
    {
        _logger.LogDebug("[Session Process Received] {SessionId} - Length: {DataLength}", SessionId, data.Length);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 소켓 연결 상태를 확인합니다. 연결이 끊어진 경우 false를 반환합니다.
    /// </summary>
    public bool IsConnectionAlive()
    {
        if (_socket == null) return false;

        try
        {
            // 소켓 상태 확인
            bool isDisconnected = CheckSocketStatus();
            
            if (isDisconnected)
            {
                _logger.LogDebug("[Connection Broken] {SessionId} detected via Poll", SessionId);
                ForceClose(DisconnectReason.SocketError); // 연결 끊김 감지 시 강제 종료
            }
            
            return !isDisconnected;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug("[Connection Exception] {SessionId}: {Message}", SessionId, ex.Message);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// 소켓 연결 상태를 확인합니다. 연결이 끊어진 경우 true를 반환합니다.
    /// </summary>
    private bool CheckSocketStatus()
    {
        // Poll이 true이고 Available이 0이면 연결이 끊어진 상태
        return _socket.Poll(1, SelectMode.SelectRead) && _socket.Available == 0;
    }

    /// <summary>
    /// 소멸자: 앱이 강제로 종료될 때 리소스를 정리합니다.
    /// </summary>
    ~SocketSession()
    {
        Dispose(false);
    }

    /// <summary>
    /// IDisposable 구현: 세션 리소스를 정리합니다.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 리소스 정리 코드의 공통 구현
    /// </summary>
    /// <param name="disposing">true: Dispose()에서 호출됨, false: 소멸자에서 호출됨</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_socket != null && _socket.Connected)
        {
            try
            {
                // 앱 종료 시에는 강제 종료 모드로 소켓 정리
                CleanupSocket(false);

                // 리소스 정리 (관리되는 리소스는 disposing이 true일 때만)
                if (disposing)
                {
                    _logger.LogDebug("[Dispose] Cleaning up session resources for {SessionId}", SessionId);
                    CleanupResources();
                }
            }
            catch (Exception ex)
            {
                // 소멸자에서는 로깅이 안전하지 않을 수 있음
                if (disposing)
                {
                    _logger.LogError(ex, "[Dispose] Error cleaning up session {SessionId}", SessionId);
                }
            }
        }
    }
}

