using Common.Network.Packet;
using Common.Network.Session;
using System.Net.Sockets;
using System.Buffers;

namespace Common.Tests.Network.Models;

/// <summary>
/// 테스트용 Packet 클래스
/// </summary>
public class Packet
{
    public PacketType PacketType { get; }
    public ReadOnlyMemory<byte> Data { get; }
    
    public Packet(PacketType packetType, byte[] data)
    {
        PacketType = packetType;
        Data = data;
    }
}

/// <summary>
/// 테스트에서 사용할 세션 이벤트 인자
/// </summary>
public class MockSessionDataEventArgs : SessionDataEventArgs
{
    public Packet Packet { get; }
    
    public MockSessionDataEventArgs(ISession session, Packet packet) 
        : base(session, packet.Data)
    {
        Packet = packet;
    }
} 