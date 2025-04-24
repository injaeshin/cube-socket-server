using Common.Network.Packet;
using Common.Network.Session;

namespace Common.Network.Handler;

public class PacketDefaultHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.None;

    public Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
