using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Network;

public abstract class SendChannel<T> : IDisposable where T : IContext
{
    protected readonly ILogger _logger;
    protected readonly Channel<T> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;
    private bool _disposed = false;

    public SendChannel(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SendChannel<T>>();

        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
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

    public async Task EnqueueAsync(T ctx)
    {
        try
        {
            await _channel.Writer.WriteAsync(ctx);
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("[SendQueue] {SessionId} Channel already closed. Packet dropped.", ctx.SessionId);
            ctx.Return();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[SendQueue] {SessionId} Failed to enqueue packet", ctx.SessionId);
            ctx.Return();
        }
    }

    protected abstract Task OnProcessAsync(T ctx);

    protected abstract void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e);

    public void Close()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _workerTask?.Wait(TimeSpan.FromSeconds(1));
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
            _cts.Dispose();
        }
    }
}