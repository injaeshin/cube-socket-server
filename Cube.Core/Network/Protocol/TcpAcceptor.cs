using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Core.Router;
using Cube.Core.Settings;

namespace Cube.Core.Network;

public class TcpAcceptor : IDisposable
{
    private readonly ILogger _logger;
    private readonly IFunctionRouter _functionRouter;
    private readonly IPoolHandler<SocketAsyncEventArgs> _poolHandler;

    private Socket _listenSocket = null!;

    private bool _closed;
    private bool _disposed;

    public TcpAcceptor(ILoggerFactory loggerFactory, IFunctionRouter functionRouter, IPoolHandler<SocketAsyncEventArgs> poolHandler)
    {
        _logger = loggerFactory.CreateLogger<TcpAcceptor>();
        _functionRouter = functionRouter;
        _poolHandler = poolHandler;

        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
    }

    public async Task RunAsync(int port)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            _listenSocket.Listen(AppSettings.Instance.Network.ListenBacklog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP 서버 시작 중 예외");
        }

        await Task.Run(DoAccept);

        _logger.LogInformation("TCP 서버 시작 (포트: {Port})", ((IPEndPoint)_listenSocket.LocalEndPoint!).Port);
    }

    public void Stop()
    {
        _closed = true;

        try
        {
            _listenSocket.Close();
            _logger.LogInformation("TCP 리스너 종료");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP 리스너 종료 중 예외");
        }
    }

    private void DoAccept()
    {
        if (_closed) return;

        //var args = new SocketAsyncEventArgs();
        //args.SetBuffer(new byte[2048]); // 버퍼 크기 설정

        var args = _poolHandler.RentWithoutBuffer();
        if (args == null)
        {
            _logger.LogError("SocketAsyncEventArgs 풀에서 가져오기 실패");
            Task.Delay(1000).ContinueWith(_ => DoAccept());             // 잠시 후 재시도
            return;
        }

        args.Completed += OnAcceptCompleted;
        args.AcceptSocket = null;

        _logger.LogTrace("AcceptAsync 호출 시작");

        bool pending;
        try
        {
            pending = _listenSocket.AcceptAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptAsync 호출 중 예외");
            _poolHandler.Return(args);
            return;
        }

        _logger.LogTrace("AcceptAsync 호출 완료 - Pending: {Pending}", pending);

        if (!pending)
        {
            _logger.LogTrace("AcceptAsync 동기 완료 - 즉시 처리");
            _ = ProcessAcceptAsync(args);
        }
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
    {
        _ = ProcessAcceptAsync(e);
    }

    private async Task ProcessAcceptAsync(SocketAsyncEventArgs e)
    {
        e.Completed -= OnAcceptCompleted;

        if (_closed)
        {
            e.AcceptSocket?.Close();
            _poolHandler.Return(e);
            return;
        }

        if (e.SocketError == SocketError.Success && e.AcceptSocket != null)
        {
            await _functionRouter.InvokeFunc<ClientAcceptedCmd, Task>(new ClientAcceptedCmd(e.AcceptSocket));
        }
        else
        {
            e.AcceptSocket?.Close();
        }

        _poolHandler.Return(e);

        if (!_closed)
        {
            DoAccept();
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
            Stop();
            _listenSocket.Dispose();
        }
    }
}