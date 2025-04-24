namespace Common.Network.Packet;

// [ 2 bytes Length ][ 2 bytes Opcode ][ Payload... ]
//       ↑                ↑
//    ushort          PacketType (ushort enum)

public enum PacketType : ushort
{
    None = 0x0000,
    Ping = 0x0001,
    Pong = 0x0002,
    Login = 0x0003,
    LoginSuccess = 0x0004,
    Logout = 0x0005,

    Chat = 0x0100,
    ChatMessage = 0x0101,

    Max,
}