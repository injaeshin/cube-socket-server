using System.Net;
using System.Net.Sockets;
using Common.Network.Session;
using Microsoft.Extensions.Logging;

namespace Common.Network;

public class SocketAcceptor : IDisposable
{
    private readonly Func<Socket, Task> _onClientConnected;
    private readonly ILogger<SocketAcceptor> _logger;
    private readonly ObjectPool<SocketAsyncEventArgs> _argsPool;
    private readonly Socket _listenSocket;

    private readonly int _port;
    private readonly int _maxConnections;

    private bool _isStopped = false;
    private bool _disposed = false;

    public SocketAcceptor(ILogger<SocketAcceptor> logger, Func<Socket, Task> onConnected, int port, int maxConnections, int listenBacklog)
    {
        _logger = logger;
        _onClientConnected = onConnected;
        _argsPool = new ObjectPool<SocketAsyncEventArgs>(() => new SocketAsyncEventArgs(), maxConnections);

        _port = port;
        _maxConnections = maxConnections;

        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        _listenSocket.Listen(listenBacklog);
    }

    public async Task Begin()
    {
        _logger.LogInformation("소켓 서버 시작 (포트: {Port}, 최대 접속자: {MaxConnections})", _port, _maxConnections);
        await Task.Run(RunAcceptAsync);
    }

    public void End()
    {
        Dispose();
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
            _isStopped = true;
            _listenSocket?.Close();
            _argsPool?.Close();
            _logger.LogInformation("소켓 리스너 종료됨");
        }
        _disposed = true;
    }

    ~SocketAcceptor()
    {
        Dispose(false);
    }

    private void RunAcceptAsync()
    {
        try
        {
            StartAccept();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept 시작 중 오류 발생");
            End();
        }
    }

    private void StartAccept()
    {
        if (_isStopped) return;

        var acceptArgs = _argsPool.Rent() ?? throw new InvalidOperationException("SocketAsyncEventArgs 풀에서 항목을 빌릴 수 없습니다");
        _logger.LogDebug("SocketAsyncEventArgs 대여됨: {HashCode}", acceptArgs.GetHashCode());

        acceptArgs.Completed += OnAcceptCompleted;
        _logger.LogDebug("Completed 이벤트 핸들러 등록됨");

        try
        {
            bool pending = _listenSocket.AcceptAsync(acceptArgs);
            _logger.LogDebug("AcceptAsync 호출 결과: pending={Pending}", pending);

            if (!pending)
            {
                _logger.LogDebug("즉시 완료됨, ProcessAccept 직접 호출");
                ProcessAccept(acceptArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptAsync 중 오류 발생");
            _argsPool.Return(acceptArgs);
            StartAccept(); // 실패해도 계속 시도
        }
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
    {
        _logger.LogDebug("OnAcceptCompleted 호출됨: SocketError={SocketError}, AcceptSocket={AcceptSocket}",
                        e.SocketError, e.AcceptSocket != null ? "있음" : "없음");
        ProcessAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        var socket = e.AcceptSocket;
        _logger.LogDebug("ProcessAccept 시작: SocketError={SocketError}, AcceptSocket={AcceptSocket}",
                        e.SocketError, socket != null ? socket.RemoteEndPoint?.ToString() : "없음");

        e.Completed -= OnAcceptCompleted;

        // 다음 Accept를 바로 시작
        StartAccept();

        if (_isStopped)
        {
            _logger.LogDebug("서버 중지 상태, 소켓 종료");
            socket?.Close();
            _argsPool.Return(e);
            return;
        }

        if (e.SocketError != SocketError.Success || socket == null)
        {
            _logger.LogWarning("소켓 오류 발생 또는 소켓 없음: {SocketError}", e.SocketError);
            _argsPool.Return(e);
            return;
        }

        // 소켓 처리
        _logger.LogDebug("클라이언트 연결 처리 시작: {RemoteEndPoint}", socket.RemoteEndPoint);
        _ = ProcessSocketAsync(socket);

        // EventArgs 반환
        e.AcceptSocket = null;
        _argsPool.Return(e);
    }

    private async Task ProcessSocketAsync(Socket socket)
    {
        try
        {
            _logger.LogDebug("ProcessSocketAsync 시작: {RemoteEndPoint}", socket.RemoteEndPoint);
            
            try
            {
                await _onClientConnected(socket);
                _logger.LogDebug("_onClientConnected 처리 완료: {RemoteEndPoint}", socket.RemoteEndPoint);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("SocketAsyncEventArgs"))
            {
                // SocketAsyncEventArgs 대여 실패 등의 리소스 부족 오류
                _logger.LogWarning("클라이언트 연결 처리를 위한 리소스 할당 실패: {RemoteEndPoint} - {Message}",
                    socket.RemoteEndPoint, ex.Message);
                
                try
                {
                    // 클라이언트에게 서버 과부하 메시지 전송 (선택 사항)
                    // 간단한 HTTP 응답형태로 보내기
                    var errorMessage = System.Text.Encoding.UTF8.GetBytes("서버가 현재 과부하 상태입니다. 잠시 후 다시 시도해주세요.");
                    await socket.SendAsync(errorMessage, SocketFlags.None);
                }
                catch { /* 무시 */ }
                
                // 소켓 정리
                try { socket.Shutdown(SocketShutdown.Both); } catch { }
                try { socket.Close(); } catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "클라이언트 연결 처리 중 오류 발생: {RemoteEndPoint}",
                socket.RemoteEndPoint != null ? socket.RemoteEndPoint.ToString() : "알 수 없음");
                
            // 정상적으로 소켓 종료 시도
            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch (Exception closeEx) 
            { 
                _logger.LogError(closeEx, "소켓 닫기 실패: {RemoteEndPoint}", 
                    socket.RemoteEndPoint != null ? socket.RemoteEndPoint.ToString() : "알 수 없음"); 
            }
        }
    }
}