using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Common.Network;

public class SocketAcceptor
{
    private readonly Func<Socket, Task> _onClientConnected;
    private readonly ILogger<SocketAcceptor> _logger;

    private bool _isStopped = false;
    private Socket _listenSocket = null!;
    private SocketAsyncEventArgs _acceptArgs = null!;

    public SocketAcceptor(ILogger<SocketAcceptor> logger, Func<Socket, Task> onConnected)
    {
        _logger = logger;
        _onClientConnected = onConnected;

        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, Constant.PORT));
        _listenSocket.Listen(Constant.MAX_CONNECTION);
    }

    public async Task Begin()
    {
        _logger.LogInformation("소켓 서버 시작 (포트: {Port})", Constant.PORT);
        await Task.Run(RunAcceptAsync);
    }

    public void End()
    {
        _isStopped = true;
        CloseListenSocket();
    }

    private void RunAcceptAsync()
    {
        try
        {
            _acceptArgs = new SocketAsyncEventArgs();
            _acceptArgs.Completed += OnAcceptCompleted;

            ContinueAccept();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept 시작 중 오류 발생");
            CloseListenSocket();
        }
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
    {
        try
        {
            _ = ProcessAccept(e);  // Fire and forget pattern
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept 완료 처리 중 오류 발생");

            if (e.AcceptSocket != null)
            {
                try { e.AcceptSocket.Close(); } catch { }
                e.AcceptSocket = null;
            }

            if (!_isStopped)
            {
                ContinueAccept();
            }
        }
    }

    private async Task ProcessAccept(SocketAsyncEventArgs e)
    {
        if (_isStopped)
        {
            return;
        }

        if (e.SocketError != SocketError.Success)
        {
            _logger.LogError("Accept 실패: {SocketError}", e.SocketError);

            if (e.AcceptSocket != null)
            {
                try { e.AcceptSocket.Close(); } catch { }
                e.AcceptSocket = null;
            }

            ContinueAccept();
            return;
        }

        try
        {
            if (e.AcceptSocket == null)
            {
                _logger.LogError("Accept 소켓이 null입니다");
                ContinueAccept();
                return;
            }
            
            await _onClientConnected(e.AcceptSocket);
            e.AcceptSocket = null;

            ContinueAccept();
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning("처분된 소켓에 접근 시도: {Message}", ex.Message);
            _logger.LogDebug("Stack trace: {StackTrace}", ex.StackTrace);

            if (!_isStopped)
            {
                ContinueAccept();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "클라이언트 소켓 처리 중 오류 발생");

            if (!_isStopped)
            {
                ContinueAccept();
            }
        }
    }

    private void ContinueAccept()
    {
        if (_isStopped || _listenSocket == null)
        {
            return;
        }

        try
        {
            if (_acceptArgs.AcceptSocket != null)
            {
                _acceptArgs.AcceptSocket = null;
            }

            bool pending = _listenSocket.AcceptAsync(_acceptArgs);
            if (!pending)
            {
                _ = ProcessAccept(_acceptArgs);  // Fire and forget pattern
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("소켓 리스너가 이미 닫혔습니다");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept 요청 중 오류 발생");
        }
    }

    private void CloseListenSocket()
    {
        if (_listenSocket != null)
        {
            try
            {
                _listenSocket.Close();
                _logger.LogInformation("소켓 리스너 종료됨");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "소켓 리스너 종료 중 오류 발생");
            }
        }
    }
}