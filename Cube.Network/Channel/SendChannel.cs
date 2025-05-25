using System.Threading.Channels;
using Microsoft.Extensions.Logging;

using System.Net.Sockets;
using Cube.Network.Pool;

namespace Cube.Network.Channel;

public abstract class SendChannel<T> : IDisposable
{
    protected readonly ILogger _logger;
    protected readonly Channel<T> _channel;
    private readonly PoolEvent _poolEvent;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;
    private bool _disposed = false;

    public SendChannel(ILoggerFactory loggerFactory, PoolEvent poolEvent)
    {
        _logger = loggerFactory.CreateLogger<SendChannel<T>>();
        _poolEvent = poolEvent;
        _cts = new CancellationTokenSource();
        _channel = System.Threading.Channels.Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _workerTask = Task.Run(WorkerAsync);
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (T ctx in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await OnProcessAsync(ctx);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[SendQueue] Worker loop cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SendQueue] Worker loop error");
        }
    }

    public async virtual Task EnqueueAsync(T packet)
    {
        await _channel.Writer.WriteAsync(packet);
    }

    protected abstract Task OnProcessAsync(T ctx);

    //     var socket = ctx.Socket;
    //     if (socket == null || !socket.Connected)
    //     {
    //         _logger.LogWarning("[SendQueue] Socket is not connected");
    //         return;
    //     }

    //     var saea = _poolEvent.OnRentEventArgs() ?? throw new Exception("SocketAsyncEventArgs is null");
    //     saea.SetBuffer(ctx.Data);
    //     saea.Completed += OnSendCompletedAsync;

    //     try
    //     {
    //         if (ctx.TransportType == TransportType.Udp)
    //         {
    //             if (ctx.RemoteEndPoint == null)
    //             {
    //                 throw new InvalidOperationException("RemoteEndPoint is null");
    //             }

    //             saea.RemoteEndPoint = ctx.RemoteEndPoint ?? throw new InvalidOperationException("RemoteEndPoint is null");
    //             if (!ctx.OnUdpPreDatagramSend(ctx.SessionId, ctx.Sequence, ctx.Data))
    //             {
    //                 return;
    //             }
    //         }
    //         else
    //         {
    //             if (socket == null)
    //             {
    //                 throw new InvalidOperationException("Socket is null");
    //             }

    //             saea.RemoteEndPoint = socket.RemoteEndPoint ?? throw new InvalidOperationException("RemoteEndPoint is null");
    //         }

    //         if (!socket.SendToAsync(saea))
    //         {
    //             OnSendCompletedAsync(null, saea);
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError(e, "[SendQueue] Error sending packet");
    //     }
    //     finally
    //     {
    //         ctx.Return();
    //     }

    //     await Task.CompletedTask;
    // }

    protected abstract void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e);

    protected SocketAsyncEventArgs RentSocketAsyncEventArgs()
    {
        var e = _poolEvent.OnRentEventArgs() ?? throw new Exception("SocketAsyncEventArgs is null");
        e.Completed += OnSendCompletedAsync;
        return e;
    }

    protected void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs e)
    {
        e.Completed -= OnSendCompletedAsync;
        e.SetBuffer(null, 0, 0);
        e.UserToken = null;
        _poolEvent.OnReleaseEventArgs(e);
    }


    public void Close()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _workerTask?.Wait(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            Close();
        }
    }
}