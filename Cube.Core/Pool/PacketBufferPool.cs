using Microsoft.Extensions.Logging;
using Cube.Packet;

namespace Cube.Core.Pool;

public class PacketBufferPool(ILoggerFactory loggerFactory, int maxConnections) : IAsyncResource
{
    private readonly BufferPool _bufferPool = new(loggerFactory, maxConnections * PacketConsts.BUFFER_SIZE, PacketConsts.BUFFER_SIZE);
    private readonly ObjectPool<PacketBuffer> _packetBufferPool = new(() => new PacketBuffer(), maxConnections);

    private volatile bool _stopped = false;

    public PacketBuffer? Rent()
    {
        if (_stopped) throw new ObjectDisposedException(nameof(PacketBufferPool));

        var rawBuffer = _bufferPool.AllocateBuffer();
        if (rawBuffer.IsEmpty)
        {
            return null;
        }

        var packetBuffer = _packetBufferPool.Rent();
        packetBuffer.Initialize(rawBuffer);
        return packetBuffer;
    }

    public void Return(PacketBuffer buffer)
    {
        if (!buffer.Memory.IsEmpty)
        {
            _bufferPool.FreeBuffer(buffer.Memory);
        }

        buffer.Clear();
        _packetBufferPool.Return(buffer);
    }

    public string Name { get; init; } = "PacketBufferPool";
    public int Count => _packetBufferPool.Count;
    public int InUse => _packetBufferPool.InUse;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) throw new InvalidOperationException("PacketBufferPool is already closed");
        _stopped = true;

        await _bufferPool.StopAsync(timeout);
        await _packetBufferPool.StopAsync(timeout);
    }
}