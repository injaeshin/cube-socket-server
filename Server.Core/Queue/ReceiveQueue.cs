using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Server.Core.Packet;

namespace Server.Core.Queue;

public class ReceiveQueue
{
    private readonly ILogger<ReceiveQueue> _logger;
    private readonly Channel<ReceivedPacket> _channel = Channel.CreateUnbounded<ReceivedPacket>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _workerTask;

    public ReceiveQueue(ILogger<ReceiveQueue> logger)
    {
        _logger = logger;
        
        _channel = Channel.CreateUnbounded<ReceivedPacket>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

        _workerTask = Task.Run(WorkerAsync);
    }

    public bool Enqueue(ReceivedPacket packet)
    {
        return _channel.Writer.TryWrite(packet);
    }

    public async Task EnqueueAsync(ReceivedPacket packet)
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
            await foreach (var packet in _channel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
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

    private async Task OnProcessAsync(ReceivedPacket packet)
    {
        try
        {
            if (packet.Session == null)
            {
                _logger.LogWarning("[RecvQueue] Packet contains null session reference");
                return;
            }

            await packet.Session.OnProcessReceivedAsync(packet.Data);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "[RecvQueue] Trying to process a packet for disposed session: {SessionId}", packet.SessionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RecvQueue] Error processing packet for session: {SessionId}", packet.SessionId);
        }
        finally
        {
            packet.Return();
        }
    }

    public async Task StopAsync()
    {
        _cancellationTokenSource.Cancel();
        _channel.Writer.Complete();
        await _workerTask;
    }
}

