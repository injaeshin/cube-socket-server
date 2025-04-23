using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Transport;
using System.Buffers;

namespace Common.Network.Session;

public class SocketSession(SocketSessionOptions options, ILogger<SocketSession> logger) : ISession, IDisposable
{
    private readonly ILogger _logger = logger;
    public string SessionId { get; private set; } = null!;

    private Socket _socket = null!;
    private SocketAsyncEventArgs _receiveArgs = null!;

    private readonly PacketBuffer _packetBuffer = new();
    private readonly SessionResource _resource = options.Resource;
    private readonly SessionQueue _queue = options.Queue;

    public event EventHandler<SessionEventArgs>? SessionConnected;
    public event EventHandler<SessionEventArgs>? SessionDisconnected;
    public event SessionDataEventHandlerAsync? SessionPreProcess;

    public void CreateSessionId()
    {
        string shortTime = DateTime.Now.ToString("HHmm");
        string randomPart = Guid.NewGuid().ToString("N")[..4];
        SessionId = $"S_{shortTime}_{randomPart}";
    }

    public void Run(Socket socket)
    {
        try
        {
            _logger.LogDebug("[Session Run] 세션 시작: {SessionId}", SessionId);

            _packetBuffer.Reset();

            ConfigureKeepAlive(socket);
            ConfigureNoDelay(socket);

            _socket = socket;
            // ReceiveArgs 대여 시도
            
            _receiveArgs = _resource.OnRentRecvArgs?.Invoke() ?? throw new InvalidOperationException("Failed to rent SocketAsyncEventArgs.");
            // ReceiveArgs가 null인 경우 더 명확한 오류 메시지와 함께 세션 정리
            if (_receiveArgs == null)
            {
                _logger.LogError("[Session Run] SocketAsyncEventArgs 대여 실패: {SessionId}", SessionId);
                CleanupSocket(false);
                throw new InvalidOperationException($"세션 {SessionId}의 수신 작업을 위한 SocketAsyncEventArgs를 대여할 수 없습니다. 서버 리소스가 부족할 수 있습니다.");
            }

            //_receiveArgs.SetBuffer(new byte[Constant.BUFFER_SIZE], 0, Constant.BUFFER_SIZE);
            _receiveArgs.Completed += OnReceiveCompleted;
            _receiveArgs.UserToken = this;


            OnConnect(this);
            SessionConnected?.Invoke(this, new SessionEventArgs(this));

            DoReceive();
        }
        catch (InvalidOperationException)
        {
            // 이미 로깅된 예외 재전파
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Session Run] 세션 시작 중 오류 발생: {SessionId}", SessionId);
            SafeCleanupResources();
            throw;
        }
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

    /// <summary>
    /// 소켓의 네이글 알고리즘 설정을 구성합니다.
    /// </summary>
    private void ConfigureNoDelay(Socket socket)
    {
        try
        {
            // TCP 소켓에 대해서만 NoDelay 설정
            if (socket.ProtocolType == ProtocolType.Tcp)
            {
                // 네이글 알고리즘 비활성화 (NoDelay = true)
                socket.NoDelay = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("네이글 알고리즘 설정 중 오류 발생: {Message}", ex.Message);
        }
    }

    private void DoReceive()
    {
        if (_socket.Connected == false)
        {
            _logger.LogWarning("[DoReceive] 소켓이 연결되지 않았습니다: {SessionId}", SessionId);
            return;
        }

        try
        {
            _logger.LogDebug("[DoReceive] ReceiveAsync 호출: {SessionId}", SessionId);
            bool pending = _socket.ReceiveAsync(_receiveArgs);
            _logger.LogDebug("[DoReceive] ReceiveAsync 결과: pending={Pending}, {SessionId}", pending, SessionId);

            if (!pending)
            {
                _logger.LogDebug("[DoReceive] 즉시 완료, OnReceiveCompleted 직접 호출: {SessionId}", SessionId);
                OnReceiveCompleted(null, _receiveArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DoReceive] ReceiveAsync 호출 중 오류 발생: {SessionId}", SessionId);
            Close(DisconnectReason.SocketError);
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
            _logger.LogDebug("[OnReceiveCompleted] 호출됨: SessionId={SessionId}, BytesTransferred={BytesTransferred}, Buffer={BufferLength}", 
                SessionId, e.BytesTransferred, e.Buffer?.Length ?? 0);

            // 연결 종료 확인
            DisconnectReason disconnectReason = GetDisconnectReason(e);
            if (disconnectReason != DisconnectReason.None)
            {
                _logger.LogDebug("[OnReceiveCompleted] 연결 종료 감지: {Reason}, {SessionId}", disconnectReason, SessionId);
                ForceClose(disconnectReason);
                return;
            }

            var data = e.MemoryBuffer[..e.BytesTransferred];
            _logger.LogDebug("[OnReceiveCompleted] 데이터 수신: {Length} 바이트, {SessionId}, 데이터: {ByteData}", 
                data.Length, SessionId, 
                BitConverter.ToString(data.Slice(0, Math.Min(data.Length, 32)).ToArray()) + (data.Length > 32 ? "..." : ""));
            
            if (!_packetBuffer.TryAppend(data))
            {
                _logger.LogWarning("[OnReceiveCompleted] 패킷 버퍼 추가 실패: {SessionId}", SessionId);
                Close(DisconnectReason.InvalidData);
                return;
            }

            while (_packetBuffer.TryReadPacket(out var packet, out var rentedBuffer))
            {
                _logger.LogDebug("[OnReceiveCompleted] 패킷 읽기 성공: {Length} 바이트, {SessionId}, 데이터: {ByteData}", 
                    packet.Length, SessionId, 
                    BitConverter.ToString(packet.Slice(0, Math.Min(packet.Length, 32)).ToArray()) + (packet.Length > 32 ? "..." : ""));
                
                // buffer = opcode + payload
                // TODO - 전처리 [1. 복호화 2. 패킷 타입 확인 3. 패킷 타입에 따른 처리] 4. 큐에 넣기
                if (SessionPreProcess == null)
                {
                    _logger.LogError("[OnReceiveCompleted] SessionPreProcess 이벤트 핸들러가 등록되지 않았습니다: {SessionId}", SessionId);
                    if (rentedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                    continue;
                }

                var preProcessResult = await SessionPreProcess.Invoke(this, new SessionDataEventArgs(this, packet));
                _logger.LogDebug("[OnReceiveCompleted] PreProcess 결과: {Result}, {SessionId}", preProcessResult, SessionId);

                // 전처리 결과에 따라 패킷 처리
                switch (preProcessResult)
                {
                    case SessionPreProcessResult.Continue:
                        // 패킷을 큐에 추가하여 계속 처리
                        await _queue.OnRecvEnqueueAsync(new ReceivedPacket(SessionId, packet, rentedBuffer, this));
                        break;

                    case SessionPreProcessResult.Discard:
                        // 패킷 처리를 중단하고 버퍼 반환
                        if (rentedBuffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedBuffer);
                        }
                        break;

                    case SessionPreProcessResult.Handled:
                        // 전처리에서 이미 처리 완료됨, 버퍼만 반환
                        if (rentedBuffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedBuffer);
                        }
                        break;
                }
            }

            DoReceive();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OnReceiveCompleted] 오류 발생: {SessionId}", SessionId);
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
        return reason == DisconnectReason.GracefulDisconnect ||
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

    public virtual Task OnProcessReceivedAsync(ReceivedPacket packet)
    {
        _logger.LogDebug("[Session Process Received] {SessionId} - Length: {DataLength}", SessionId, packet.Data.Length);
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
                    // 패킷 버퍼 초기화
                    ResetBuffer();
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

    /// <summary>
    /// 패킷 버퍼를 초기화합니다.
    /// </summary>
    public void ResetBuffer()
    {
        _packetBuffer.Reset();
        _logger.LogDebug("[Session Reset] 패킷 버퍼 초기화: {SessionId}", SessionId);
    }
}

