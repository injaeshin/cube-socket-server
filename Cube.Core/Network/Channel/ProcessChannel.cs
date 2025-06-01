using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Network;

public sealed class ProcessChannel<T> : IDisposable where T : IContext
{
    private readonly ILogger _logger;
    private readonly Channel<T> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;

    private readonly Func<T, Task> _onProcess;
    private bool _disposed = false;

    public ProcessChannel(ILoggerFactory loggerFactory, Func<T, Task> onProcess)
    {
        _logger = loggerFactory.CreateLogger<ProcessChannel<T>>();
        _onProcess = onProcess;

        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _workerTask = Task.Run(WorkerAsync);
    }

    public async Task EnqueueAsync(T ctx)
    {
        try
        {
            await _channel.Writer.WriteAsync(ctx);
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("[RecvQueue] {SessionId} Channel already closed. Packet dropped.", ctx.SessionId);
            ctx.Return();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] {SessionId} Failed to enqueue packet", ctx.SessionId);
            ctx.Return();
        }
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var ctx in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await OnProcessAsync(ctx);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[RecvQueue] Worker loop cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] Worker loop error");
        }
    }

    private async Task OnProcessAsync(T ctx)
    {
        try
        {
            await _onProcess(ctx);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] {SessionId} Failed to process packet", ctx.SessionId);
            ctx.Return();
        }
    }

    public void Close()
    {
        if (_disposed) return;
        if (_workerTask.IsCompleted) return;
        _disposed = true;

        _cts.Cancel();
        _channel.Writer.TryComplete();
        _workerTask.Wait(TimeSpan.FromSeconds(1));
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

