using Cube.Common.Interface;

namespace Cube.Packet;

public class DefaultPacketHandler : PayloadPacketHandlerBase
{
    public override PacketType Type => PacketType.None;

    public override Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }

    public override Task<bool> HandleAsync(ISession session)
    {
        return Task.FromResult(false);
    }
}
