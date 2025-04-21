using System.Net.Sockets;
using System.Threading.Channels;

using Common.Network.Transport;

namespace Common.Network.Queue;

public class SendQueue
{
    private readonly Channel<SendRequest> _channel = Channel.CreateUnbounded<SendRequest>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _worker;

    public SendQueue()
    {
        _channel = Channel.CreateUnbounded<SendRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

        _worker = Task.Run(WorkerAsync);
    }

    public bool Enqueue(SendRequest packet)
    {
        return _channel.Writer.TryWrite(packet);
    }

    public async Task EnqueueAsync(SendRequest packet)
    {
        try
        {
            await _channel.Writer.WriteAsync(packet);
        }
        catch (ChannelClosedException)
        {
            Console.WriteLine($"[SendQueue] {packet.SessionId} Channel already closed. Packet dropped.");
            packet.Return();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[SendQueue] {packet.SessionId} Failed to enqueue packet: {e}");
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
            Console.WriteLine("[SendQueue] Worker loop cancelled");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[SendQueue] Worker loop error: {e}");
        }
    }

    private async Task OnProcessAsync(SendRequest packet)
    {
        try
        {
            if (packet.Socket.Connected == false)
            {
                Console.WriteLine($"[SendQueue] {packet.SessionId} Socket is not connected. Packet dropped.");
                return;
            }

            await packet.Socket.SendAsync(packet.Data, SocketFlags.None, _cancellationTokenSource.Token);
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
        await _worker;
    }
}

