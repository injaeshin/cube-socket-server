using Common.Protocol;
using Server.Core.Session;

namespace Server.Core.Packet;

public class SendPacket
{
    public ISocketSession Session { get; }

    public PacketType Type { get; }
    public ReadOnlyMemory<byte> Payload { get; }

    public SendPacket(ISocketSession session, PacketType type, ReadOnlyMemory<byte> payload)
    {
        Session = session;
        Type = type;
        Payload = payload;
    }

    //public Task ReplyAsync(ReadOnlyMemory<byte> data)
    //{
    //    return Session.SendAsync(Type, data);
    //}
}