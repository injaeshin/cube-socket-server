using System.Threading.Channels;
using Microsoft.Extensions.Logging;

using Common.Network.Packet;

namespace Common.Network.Buffer;

public class ReceiveChannel : IDisposable
{
    private bool _disposed = false;
    private readonly ILogger<ReceiveChannel> _logger;
    private Channel<ReceivedPacket> _channel = null!;
    private CancellationTokenSource _cts = null!;
    private Task _workerTask = null!;

    public ReceiveChannel(ILogger<ReceiveChannel> logger)
    {
        _logger = logger;
    }

    public void Run()
    {
        Reset();

        _workerTask = Task.Run(WorkerAsync);
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

    private async Task OnProcessAsync(ReceivedPacket packet)
    {
        try
        {
            if (packet.Session == null)
            {
                _logger.LogWarning("[RecvQueue] Packet contains null session reference");
                return;
            }

            if (packet.Session.IsConnectionAlive())
            {
                await packet.Session.OnProcessReceivedAsync(packet);
            }
            else
            {
                packet.Session.Close(DisconnectReason.ApplicationRequest);
                _logger.LogWarning("[RecvQueue] Session is not alive: {SessionId}", packet.SessionId);
            }
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

    public void Reset()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ReceiveChannel));

        _cts?.Cancel();
        _workerTask?.GetAwaiter().GetResult();
        _channel?.Writer.Complete();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<ReceivedPacket>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _workerTask?.GetAwaiter().GetResult();
            _channel.Writer.Complete();

            _cts.Dispose();
            _disposed = true;
        }
    }
}

