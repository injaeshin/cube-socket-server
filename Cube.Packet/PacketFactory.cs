using Cube.Common.Interface;

namespace Cube.Packet;

public class PacketFactory : IPacketFactory
{
    public (Memory<byte> data, byte[]? rentedBuffer) CreateChatMessagePacket(string sender, string message)
    {
        var packet = new PacketWriter().WriteType((ushort)PacketType.ChatMessage);
        packet.WriteString(sender);
        packet.WriteString(message);
        return packet.ToTcpPacket();
    }

    public (Memory<byte> data, byte[]? rentedBuffer) CreatePingPacket()
    {
        var packet = new PacketWriter().WriteType((ushort)PacketType.Ping);
        return packet.ToTcpPacket();
    }
}
