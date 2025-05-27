using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;

namespace Cube.Core.Network;

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