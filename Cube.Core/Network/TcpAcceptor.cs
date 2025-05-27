using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Common;

namespace Cube.Core.Network;

public class TcpAcceptor : IDisposable
{
    private readonly ILogger _logger;
    private Socket _listenSocket = null!;
    private readonly SocketAsyncEventArgsPool _saeaPool;

    private readonly Func<Socket, Task> _onClientConnected;

    private bool _isStopped;
    private bool _disposed;

    public TcpAcceptor(ILoggerFactory loggerFactory, Func<Socket, Task> onConnected)
    {
        _logger = loggerFactory.CreateLogger<TcpAcceptor>();
        _saeaPool = new SocketAsyncEventArgsPool(loggerFactory, Consts.LISTEN_BACKLOG);
        _onClientConnected = onConnected;
    }

    public async Task Run(int port)
    {
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        _listenSocket.Listen(Consts.LISTEN_BACKLOG);

        DoAccept();
        _logger.LogInformation("TCP 서버 시작 (포트: {Port})", ((IPEndPoint)_listenSocket.LocalEndPoint!).Port);
        await Task.CompletedTask;
    }

    private void DoAccept()
    {
        if (_isStopped) return;

        var args = _saeaPool.RentWithoutBuffer();
        if (args == null)
        {
            _logger.LogError("SocketAsyncEventArgs 풀에서 가져오기 실패");
            Task.Delay(1000).ContinueWith(_ => DoAccept());             // 잠시 후 재시도
            return;
        }

        //args.Completed -= OnAcceptCompleted;
        args.Completed += OnAcceptCompleted;
        args.AcceptSocket = null;

        _logger.LogDebug("AcceptAsync 호출 시작");

        bool pending;
        try
        {
            pending = _listenSocket.AcceptAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptAsync 호출 중 예외");
            _saeaPool.Return(args);
            return;
        }

        _logger.LogDebug("AcceptAsync 호출 완료 - Pending: {Pending}", pending);

        if (!pending)
        {
            _logger.LogDebug("AcceptAsync 동기 완료 - 즉시 처리");
            ProcessAccept(args);
        }
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        e.Completed -= OnAcceptCompleted;

        if (_isStopped)
        {
            e.AcceptSocket?.Close();
            _saeaPool.Return(e);
            return;
        }

        if (e.SocketError == SocketError.Success && e.AcceptSocket != null)
        {
            _ = _onClientConnected(e.AcceptSocket);
        }
        else
        {
            e.AcceptSocket?.Close();
        }

        _saeaPool.Return(e);

        if (!_isStopped)
        {
            DoAccept();
        }
    }

    public void Stop()
    {
        _isStopped = true;
        _listenSocket.Close();
        _saeaPool.Close();
        _logger.LogInformation("TCP 리스너 종료");
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
            Stop();
            _listenSocket.Dispose();
        }
    }
}