using Cube.Common.Shared;
using Cube.Session;

namespace Cube.Handler;

public class DefaultPacketHandler : IPacketHandler
{
    public PacketType Type => PacketType.None;

    public Task<bool> HandleAsync(INetSession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
