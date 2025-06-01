using Cube.Packet;

namespace Cube.Core.Execution;

public class DefaultPacketHandler : IPacketHandler
{
    public PacketType Type => PacketType.None;

    public Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
