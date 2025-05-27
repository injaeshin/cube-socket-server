using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Common;

namespace Cube.Core.Network;

public class UdpReceiver : IDisposable
{
    private static readonly string WELCOME_MESSAGE = "knock-knock";

    private readonly ILogger _logger;
    private readonly SocketAsyncEventArgsPool _saeaPool;
    private readonly TransportUdpReceiveHandler _receiveEvent;

    private Socket _receiveSocket = null!;
    private IPEndPoint _localEndPoint = null!;

    private bool _isClosed;
    private bool _disposed;
    public UdpReceiver(ILoggerFactory loggerFactory, TransportUdpReceiveHandler receiveEvent)
    {
        _logger = loggerFactory.CreateLogger<UdpReceiver>();
        _saeaPool = new SocketAsyncEventArgsPool(loggerFactory, Consts.MAX_UDP_CONNECTIONS);
        _receiveEvent = receiveEvent;
    }

    public async Task Run(int port)
    {
        _localEndPoint = new IPEndPoint(IPAddress.Any, port);

        _receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _receiveSocket.Bind(_localEndPoint);

        DoReceive();
        _logger.LogInformation("UDP 서버 시작 (포트: {Port})", port);
        await Task.CompletedTask;
    }



    public void DoReceive()
    {
        if (_isClosed)
        {
            return;
        }

        try
        {
            var args = _saeaPool.Rent() ?? throw new InvalidOperationException("SocketAsyncEventArgs 풀 오류");
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
            _saeaPool.Return(e);
            return;
        }

        if (e.SocketError != SocketError.Success || e.RemoteEndPoint == null)
        {
            _saeaPool.Return(e);
            return;
        }

        try
        {
            if (e.BytesTransferred > 4)
            {
                var buffer = e.Buffer.AsSpan(e.Offset, e.BytesTransferred);
                if (!BufferArrayPool.TryRent(buffer, e.BytesTransferred, out var data, out var rentedBuffer))
                {
                    _logger.LogError("BufferArrayPool 풀 오류");
                    return;
                }

                var remote = new IPEndPoint(((IPEndPoint)e.RemoteEndPoint!).Address, ((IPEndPoint)e.RemoteEndPoint!).Port);
                _ = HandleReceiveAsync(remote, data, rentedBuffer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error udp receiving data");
        }
        finally
        {
            _saeaPool.Return(e);
        }

        DoReceive();
    }

    private async Task HandleReceiveAsync(EndPoint remote, ReadOnlyMemory<byte> data, byte[]? rentedBuffer)
    {
        try
        {
            if (data.Length < WELCOME_MESSAGE.Length)
            {
                _logger.LogError("Invalid data length: {Length}", data.Length);
                return;
            }

            if (data.Span[4..].SequenceEqual(Encoding.UTF8.GetBytes(WELCOME_MESSAGE)))
            {
                string sessionId = Encoding.UTF8.GetString(data.Span[..4]);
                _receiveEvent.OnUdpGreetingReceived(remote, sessionId);
            }
            else
            {
                _receiveEvent.OnUdpDatagramReceived(remote, data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving data");
        }
        finally
        {
            BufferArrayPool.Return(rentedBuffer);
        }

        await Task.CompletedTask;
    }

    public void Stop()
    {
        _isClosed = true;
        _receiveSocket.Close();
        _saeaPool.Close();

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

