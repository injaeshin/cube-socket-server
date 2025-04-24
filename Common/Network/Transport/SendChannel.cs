using System.Net.Sockets;
using System.Threading.Channels;
using System.Buffers;
using Common.Network.Packet;

namespace Common.Network.Transport;

public class SendChannel
{
    private readonly PacketBuffer _sendBuffer = new();
    private readonly Channel<bool> _trigger = Channel.CreateUnbounded<bool>();
    private readonly CancellationTokenSource _cts = new();
    
    private Socket? _socket;
    private Task? _worker;

    public async Task EnqueueAsync(ReadOnlyMemory<byte> packet)
    {
        if (!_sendBuffer.TryAppend(packet))
            throw new InvalidOperationException("SendBuffer full");
        await _trigger.Writer.WriteAsync(true);
    }

    private async Task WorkerAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await _trigger.Reader.ReadAsync(_cts.Token);

            while (_sendBuffer.TryRead(1400, out var chunk, out var rentedBuffer))
            {
                try
                {
                    int sent = await _socket!.SendAsync(chunk, SocketFlags.None, _cts.Token);
                    _sendBuffer.Skip(sent);
                }
                finally
                {
                    if (rentedBuffer != null)
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }
    }

    public void Run(Socket socket)
    {
        _sendBuffer.Reset();
        _socket = socket;
        _worker = Task.Run(WorkerAsync);
    }

    public void Stop()
    {
        _cts.Cancel();
        _trigger.Writer.Complete();
    }
}

