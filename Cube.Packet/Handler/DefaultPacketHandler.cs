using Cube.Common.Interface;

namespace Cube.Packet;

public class DefaultPacketHandler : IPacketHandler
{
    public PacketType Type => PacketType.None;

    public Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
