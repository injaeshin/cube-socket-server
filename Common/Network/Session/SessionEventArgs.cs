using Common.Network.Packet;

namespace Common.Network.Session;

public class SessionEventArgs(ISession session) : EventArgs
{
    public ISession Session { get; } = session;
}

public class SessionDataEventArgs(ISession session, PacketType type, ReadOnlyMemory<byte> data) : SessionEventArgs(session)
{
    public PacketType PacketType { get; } = type;
    public ReadOnlyMemory<byte> Data { get; } = data;
}
