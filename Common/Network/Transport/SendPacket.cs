using Common.Network.Packet;
using Common.Network.Session;

namespace Common.Network.Transport;

public class SendPacket(ISession session, PacketType type, ReadOnlyMemory<byte> payload)
{
    public ISession Session { get; } = session;

    public PacketType Type { get; } = type;
    public ReadOnlyMemory<byte> Payload { get; } = payload;
}