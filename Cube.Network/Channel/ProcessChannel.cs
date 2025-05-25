using System.Threading.Channels;
using Microsoft.Extensions.Logging;

using Cube.Network.Context;

namespace Cube.Network.Channel;

public sealed class ProcessChannel : IDisposable
{
    private readonly ILogger _logger;
    private readonly Channel<ReceivedContext> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;
    private readonly Func<ReceivedContext, Task> _onProcess;

    private bool _disposed = false;

    public ProcessChannel(ILoggerFactory loggerFactory, Func<ReceivedContext, Task> onProcess)
    {
        _logger = loggerFactory.CreateLogger<ProcessChannel>();
        _cts = new CancellationTokenSource();
        _channel = System.Threading.Channels.Channel.CreateUnbounded<ReceivedContext>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _workerTask = Task.Run(WorkerAsync);
        _onProcess = onProcess;
    }

    public async Task EnqueueAsync(ReceivedContext packet)
    {
        try
        {
            await _channel.Writer.WriteAsync(packet);
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("[RecvQueue] {SessionId} Channel already closed. Packet dropped.", packet.SessionId);
            packet.Return();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] {SessionId} Failed to enqueue packet", packet.SessionId);
            packet.Return();
        }
    }

    private async Task WorkerAsync()
    {
        try
        {
            await foreach (var packet in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await OnProcessAsync(packet);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[RecvQueue] Worker loop cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] Worker loop error");
        }
    }

    private async Task OnProcessAsync(ReceivedContext packet)
    {
        try
        {
            await _onProcess(packet);
        }
        finally
        {
            packet.Return();
        }
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

