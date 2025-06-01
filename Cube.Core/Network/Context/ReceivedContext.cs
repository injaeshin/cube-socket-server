using System.Net;
using Cube.Packet;

namespace Cube.Core.Network;

public class ReceivedContext : IContext
{
    public string SessionId { get; init; }
    public PacketType PacketType { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
    public byte[]? RentedBuffer { get; init; }

    public ReceivedContext(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload, byte[]? rentedBuffer)
    {
        SessionId = sessionId;
        PacketType = packetType;
        Payload = payload;
        RentedBuffer = rentedBuffer;
    }

    public void Return()
    {
        if (RentedBuffer != null)
        {
            BufferArrayPool.Return(RentedBuffer);
        }
    }
}

public class UdpReceivedContext : ReceivedContext
{
    public EndPoint RemoteEndPoint { get; init; }
    public ushort Sequence { get; init; }
    public ushort Ack { get; init; }
    public UdpReceivedContext(EndPoint remoteEndPoint,
                                string sessionId,
                                ushort sequence,
                                ushort ack,
                                PacketType packetType,
                                ReadOnlyMemory<byte> payload,
                                byte[]? rentedBuffer)
        : base(sessionId, packetType, payload, rentedBuffer)
    {
        RemoteEndPoint = remoteEndPoint;
        Sequence = sequence;
        Ack = ack;
    }
}
