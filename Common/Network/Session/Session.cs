using System.Buffers;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Common.Network.Buffer;
using Common.Network.Packet;

namespace Common.Network.Session;

public interface ISession : IDisposable
{
    string SessionId { get; }
    Socket Socket { get; }
    SocketAsyncEventArgs ReceiveArgs { get; }
    bool IsConnectionAlive();

    void OnRent();

    void Run(Socket socket, SocketAsyncEventArgs receiveArgs);
    void Close(DisconnectReason reason = DisconnectReason.ApplicationRequest, bool force = false);
    Task SendAsync(ReadOnlyMemory<byte> packet);
    Task OnProcessReceivedPacketAsync(ReceivedPacket packet);
}

public class Session(ILoggerFactory loggerFactory) : ISession, IDisposable
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<Session>();
    private readonly PacketBuffer _receiveBuffer = new();
    private readonly ReceiveChannel _recvChannel = new(loggerFactory.CreateLogger<ReceiveChannel>());
    private readonly SendChannel _sendChannel = new();
    private Socket _socket = null!;
    public Socket Socket => _socket;
    private SocketAsyncEventArgs _receiveArgs = null!;
    public SocketAsyncEventArgs ReceiveArgs => _receiveArgs;

    private string sessionId = string.Empty;
    public string SessionId => sessionId;

    private static string CreateSessionId()
    {
        string shortTime = DateTime.Now.ToString("HHmm");
        string randomPart = Guid.NewGuid().ToString("N")[..4];
        return $"S_{shortTime}_{randomPart}";
    }

    public void OnRent()
    {
        sessionId = CreateSessionId();
    }

    public void Run(Socket socket, SocketAsyncEventArgs receiveArgs)
    {
        try
        {
            _logger.LogDebug("[Session Run] 세션 시작: {SessionId}", SessionId);
            _receiveBuffer.Reset();

            ConfigureKeepAlive(socket);
            ConfigureNoDelay(socket);

            _socket = socket;
            _receiveArgs = receiveArgs;
            _receiveArgs.Completed += OnReceiveCompleted;
            _receiveArgs.UserToken = this;
            OnConnected(this);
            DoReceive();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Session Run] 세션 시작 중 오류 발생: {SessionId}", SessionId);
            CleanupResources();
            throw;
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

    private async void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        try
        {
            _logger.LogDebug("[OnReceiveCompleted] 호출됨: SessionId={SessionId}, BytesTransferred={BytesTransferred}, Buffer={BufferLength}", SessionId, e.BytesTransferred, e.Buffer?.Length ?? 0);
            DisconnectReason disconnectReason = GetDisconnectReason(e);
            if (disconnectReason != DisconnectReason.None)
            {
                _logger.LogDebug("[OnReceiveCompleted] 연결 종료 감지: {Reason}, {SessionId}", disconnectReason, SessionId);
                Close(disconnectReason);
                return;
            }

            var data = e.MemoryBuffer[..e.BytesTransferred];
            _logger.LogDebug("[OnReceiveCompleted] 데이터 수신: {Length} 바이트, {SessionId}, 데이터: {ByteData}",
                                data.Length, SessionId, BitConverter.ToString(data.Slice(0, Math.Min(data.Length, 32)).ToArray()) + (data.Length > 32 ? "..." : ""));

            if (!_receiveBuffer.TryAppend(data))
            {
                _logger.LogWarning("[OnReceiveCompleted] 패킷 버퍼 추가 실패: {SessionId}", SessionId);
                Close(DisconnectReason.InvalidData);
                return;
            }

            while (_receiveBuffer.TryReadPacket(out var packetType, out var packet, out var rentedBuffer))
            {
                try
                {
                    await OnPacketReceived(new ReceivedPacket(SessionId, packetType, packet, rentedBuffer, this));
                    _logger.LogDebug("[OnReceiveCompleted] 패킷 처리: {SessionId}", SessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[OnReceiveCompleted] 패킷 처리 중 오류 발생: {SessionId}", SessionId);
                }
                finally
                {
                    if (rentedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
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

    public void Close(DisconnectReason reason = DisconnectReason.ApplicationRequest, bool force = false)
    {
        if (!force && (_socket == null || !_socket.Connected))
            return;
        try
        {
            LogDisconnection(reason);
            var isGraceful = IsGracefulDisconnect(reason, force);
            CleanupSession(reason, isGraceful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session close: {Reason}", reason);
            CleanupResources();
        }
    }

    private static bool IsGracefulDisconnect(DisconnectReason reason, bool force)
    {
        if (force)
            return false;
        return reason == DisconnectReason.GracefulDisconnect
            || reason == DisconnectReason.ApplicationRequest
            || reason == DisconnectReason.ServerShutdown;
    }

    private void CleanupSession(DisconnectReason reason, bool graceful)
    {
        CleanupSocket(graceful);
        CleanupResources();
        OnDisconnected(this, reason);
    }

    private void CleanupSocket(bool graceful)
    {
        try
        {
            if (graceful && _socket != null && _socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            _socket?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Socket cleanup error");
        }
    }

    private void CleanupResources()
    {
        if (_receiveArgs != null)
        {
            _receiveArgs.Completed -= OnReceiveCompleted;
            _receiveArgs.UserToken = null;
        }

        _receiveBuffer.Reset();
    }

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

    #region 이벤트 및 패킷 처리
    protected virtual void OnConnected(ISession session)
    {
        _logger.LogInformation("OnConnected: {SessionId}", SessionId);
        _sendChannel.Run(Socket);
    }
    protected virtual void OnDisconnected(ISession session, DisconnectReason reason)
    {
        _logger.LogInformation("OnDisconnected: {SessionId} - 이유: {Reason}", SessionId, reason);
        _sendChannel.Stop();
    }
    protected virtual async Task OnPacketReceived(ReceivedPacket packet)
    {
        _logger.LogInformation("OnPacketReceived: {SessionId}", SessionId);
        await Task.CompletedTask;
    }
    public virtual async Task OnProcessReceivedPacketAsync(ReceivedPacket packet)
    {
        _logger.LogInformation("OnProcessReceivedPacketAsync: {SessionId}", SessionId);
        await Task.CompletedTask;
    }
    public virtual async Task SendAsync(ReadOnlyMemory<byte> packet)
    {
        _logger.LogInformation("SendAsync: {SessionId}", SessionId);
        await _sendChannel.EnqueueAsync(packet);
    }
    #endregion

    protected async Task EnqueueSendAsync(ReadOnlyMemory<byte> packet)
    {
        await _sendChannel.EnqueueAsync(packet);
    }

    protected async Task EnqueueRecvAsync(ReceivedPacket packet)
    {
        await _recvChannel.EnqueueAsync(packet);
    }

    #region 소켓 설정 및 유틸리티
    private static void ConfigureKeepAlive(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        int keepAliveTime = 15;
        int keepAliveInterval = 5;
        int keepAliveRetryCount = 3;
        if (OperatingSystem.IsWindows())
        {
            byte[] keepAliveValues = new byte[12];
            BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0);
            BitConverter.GetBytes(keepAliveTime * 1000).CopyTo(keepAliveValues, 4);
            BitConverter.GetBytes(keepAliveInterval * 1000).CopyTo(keepAliveValues, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTime);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveInterval);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, keepAliveRetryCount);
        }
    }
    private static void ConfigureNoDelay(Socket socket)
    {
        if (socket.ProtocolType == ProtocolType.Tcp)
        {
            socket.NoDelay = true;
        }
    }
    private static DisconnectReason GetDisconnectReason(SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
            return DisconnectReason.SocketError;
        if (e.LastOperation == SocketAsyncOperation.Receive && e.BytesTransferred == 0)
            return DisconnectReason.GracefulDisconnect;
        return DisconnectReason.None;
    }
    #endregion

    #region 상태 확인
    public bool IsConnectionAlive()
    {
        if (_socket == null) return false;
        try
        {
            bool isDisconnected = _socket.Poll(1, SelectMode.SelectRead) && _socket.Available == 0;
            if (isDisconnected)
            {
                _logger.LogDebug("[Connection Broken] {SessionId} detected via Poll", SessionId);
                Close(DisconnectReason.SocketError);
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
    #endregion

    #region IDisposable 구현
    ~Session()
    {
        Dispose(false);
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (_socket != null && _socket.Connected)
        {
            try
            {
                CleanupSocket(false);
                if (disposing)
                {
                    _logger.LogDebug("[Dispose] Cleaning up session resources for {SessionId}", SessionId);
                    CleanupResources();
                }
            }
            catch (Exception ex)
            {
                if (disposing)
                {
                    _logger.LogError(ex, "[Dispose] Error cleaning up session {SessionId}", SessionId);
                }
            }
        }
    }
    #endregion
}

