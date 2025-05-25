using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Network.Pool;

namespace Cube.Network.Acceptor;

public class TcpAcceptor : IDisposable
{
    private readonly ILogger _logger;
    private readonly Func<Socket, Task> _onClientConnected;
    private readonly PoolEvent _saeaPoolEvent;
    private Socket _listenSocket = null!;
    private bool _isStopped;
    private bool _disposed;

    public TcpAcceptor(ILoggerFactory loggerFactory, PoolEvent saeaPoolEvent, Func<Socket, Task> onConnected)
    {
        _logger = loggerFactory.CreateLogger<TcpAcceptor>();
        _saeaPoolEvent = saeaPoolEvent;
        _onClientConnected = onConnected;
    }

    public async Task Run(int port, int backlog = 512)
    {
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        _listenSocket.Listen(backlog);

        DoAccept();
        _logger.LogInformation("TCP 서버 시작 (포트: {Port})", ((IPEndPoint)_listenSocket.LocalEndPoint!).Port);
        await Task.CompletedTask;
    }

    private void DoAccept()
    {
        if (_isStopped) return;

        var args = _saeaPoolEvent.OnRentEventArgs() ?? throw new InvalidOperationException("SocketAsyncEventArgs 풀 오류");
        args.Completed += OnAcceptCompleted;

        if (!_listenSocket.AcceptAsync(args))
        {
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
            _saeaPoolEvent.OnReleaseEventArgs(e);
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

        _saeaPoolEvent.OnReleaseEventArgs(e);

        if (!_isStopped)
        {
            DoAccept();
        }
    }

    public void Stop()
    {
        _isStopped = true;
        _listenSocket.Close();
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