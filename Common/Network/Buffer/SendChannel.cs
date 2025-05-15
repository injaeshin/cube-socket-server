using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Common.Network.Buffer;

public sealed class SendChannel : IDisposable
{
    private readonly ILogger _logger;
    private readonly PacketBuffer _sendBuffer;
    private Channel<bool> _channel;
    private CancellationTokenSource _cts;
    private Socket? _socket;
    private Task? _worker;

    public SendChannel(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SendChannel>();
        _sendBuffer = new PacketBuffer(NetConsts.BUFFER_SIZE);
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public void Run(Socket socket)
    {
        _socket = socket;
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _worker = Task.Run(WorkerAsync);
    }

    public async Task EnqueueAsync(ReadOnlyMemory<byte> packet)
    {
        if (!_sendBuffer.TryAppend(packet))
            return;

        await _channel.Writer.WriteAsync(true);
    }

    private async Task WorkerAsync()
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_channel.Reader.TryRead(out _))
                {
                    await FlushSendBufferAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendChannel worker error");
        }
    }

    private async Task FlushSendBufferAsync()
    {
        while (_sendBuffer.DataSize() > 0 && !_cts.IsCancellationRequested)
        {
            if (!_sendBuffer.TryReadAsContinuous(NetConsts.SEND_CHUNK_SIZE, out var chunk, out var rentedBuffer))
                break;

            try
            {
                if (_socket == null || !_socket.Connected)
                    return;

                int sent = await _socket.SendAsync(chunk, SocketFlags.None, _cts.Token);
                if (sent > 0)
                    _sendBuffer.Skip(sent);
            }
            catch (SocketException e)
            {
                _logger.LogWarning("Socket send error: {Error}", e.SocketErrorCode);
                break;
            }
            finally
            {
                PacketBuffer.ReturnBuffer(rentedBuffer);
            }
        }
    }

    public void Reset()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        try
        {
            _cts.Cancel();
            _channel.Writer.TryComplete();
            _worker?.Wait(TimeSpan.FromSeconds(1));
        }
        catch { }
        finally
        {
            _cts.Dispose();
            _worker = null;
            _socket = null;
            _sendBuffer.Reset();
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}