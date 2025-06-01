using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Core.Router;
using Cube.Packet;

namespace Cube.Core.Network;

public class UdpReceiver : IDisposable
{
    private readonly ILogger _logger;
    private readonly IFunctionRouter _functionRouter;
    private readonly IPoolHandler<SocketAsyncEventArgs> _poolHandler;
    private readonly ProcessChannel<UdpReceivedContext> _processChannel;

    private Socket _receiveSocket = null!;
    private IPEndPoint _localEndPoint = null!;

    private bool _isClosed;
    private bool _disposed;

    public UdpReceiver(ILoggerFactory loggerFactory, IFunctionRouter functionRouter, IPoolHandler<SocketAsyncEventArgs> poolHandler)
    {
        _logger = loggerFactory.CreateLogger<UdpReceiver>();
        _functionRouter = functionRouter;
        _poolHandler = poolHandler;

        _processChannel = new(loggerFactory, OnProcessAsync);
    }

    public async Task RunAsync(int port)
    {
        _localEndPoint = new IPEndPoint(IPAddress.Any, port);

        _receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _receiveSocket.Bind(_localEndPoint);

        DoReceive();
        _logger.LogInformation("UDP 서버 시작 (포트: {Port})", port);
        await Task.CompletedTask;
    }

    private void DoReceive()
    {
        if (_isClosed)
        {
            return;
        }

        try
        {
            var args = _poolHandler.Rent() ?? throw new InvalidOperationException("SocketAsyncEventArgs 풀 오류");
            args.RemoteEndPoint = _localEndPoint;
            args.Completed += OnReceiveCompleted;

            _receiveSocket.ReceiveFromAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving data");
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        e.Completed -= OnReceiveCompleted;

        if (_isClosed)
        {
            e.SocketError = SocketError.OperationAborted;
            _poolHandler.Return(e);
            return;
        }

        if (e.SocketError != SocketError.Success || e.RemoteEndPoint == null)
        {
            _poolHandler.Return(e);
            return;
        }

        try
        {
            ReadOnlyMemory<byte> buffer = e.MemoryBuffer[..e.BytesTransferred];
            if (!buffer.TryGetValidateUdpPacket(out var token, out var sequence, out var ack, out var packetType, out var payload, out var rentedBuffer))
            {
                _logger.LogError("Invalid udp received data: {Data}", buffer);
                return;
            }

            var ctx = new UdpReceivedContext(e.RemoteEndPoint!, token, sequence, ack, packetType, payload, rentedBuffer);
            _ = HandleReceiveAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error udp receiving data");
        }
        finally
        {
            _poolHandler.Return(e);
        }

        DoReceive();
    }

    private async Task HandleReceiveAsync(UdpReceivedContext ctx)
    {
        try
        {
            await _processChannel.EnqueueAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving data");
            ctx.Return();
        }
    }

    private async Task OnProcessAsync(UdpReceivedContext ctx)
    {
        try
        {
            await _functionRouter.InvokeFunc<ClientDatagramReceivedCmd, Task>(new ClientDatagramReceivedCmd(ctx));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data");
            ctx.Return();
        }
    }

    public void Stop()
    {
        _isClosed = true;
        _receiveSocket.Close();

        _logger.LogInformation("UDP 서버 종료");
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
            _receiveSocket.Dispose();
        }
    }
}

