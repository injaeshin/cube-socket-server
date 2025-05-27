// [ 2 bytes Length ][ 2 bytes MessageType ][ Payload... ]
//       ↑                ↑
//    ushort          MessageType (ushort enum)

namespace Cube.Packet;

public enum PacketDomain : ushort
{
    Auth = 0x0000,
    Chat = 0x0100,
    Channel = 0x0200,
}

public enum PacketType : ushort
{
    None = 0x0000,

    // Auth Domain (0x0000 ~ 0x00FF)
    Ping = 0x0001,
    Pong = 0x0002,
    Login = 0x0003,
    LoginSuccess = 0x0004,
    Logout = 0x0005,
    LogoutSuccess = 0x0006,

    // Chat Domain (0x0100 ~ 0x01FF)
    Chat = 0x0100,
    ChatMessage = 0x0101,

    // Channel Domain (0x0200 ~ 0x02FF)
    Channel = 0x0200,
    ChannelJoin = 0x0201,
    ChannelLeave = 0x0202,
    ChannelMessage = 0x0203,

    Max,
}

public static class PacketTypeExtensions
{
    public static PacketDomain GetPacketDomain(this PacketType type)
    {
        // 0x0001 >> 8 = 0x00 (Auth 도메인)
        // 0x0100 >> 8 = 0x01 (Chat 도메인)
        // 0x0200 >> 8 = 0x02 (Channel 도메인)
        return ((int)type >> 8) switch
        {
            0x00 => PacketDomain.Auth,
            0x01 => PacketDomain.Chat,
            0x02 => PacketDomain.Channel,
            _ => throw new ArgumentException($"Invalid packet type: {type}"),
        };
    }
}