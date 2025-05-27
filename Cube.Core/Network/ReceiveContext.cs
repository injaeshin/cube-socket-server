using System.Buffers;

namespace Cube.Core.Network;

public class ReceivedContext
{
    public string SessionId { get; init; }
    public ushort PacketType { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
    public byte[]? RentedBuffer { get; init; }

    public ReceivedContext(string sessionId, ushort packetType, ReadOnlyMemory<byte> payload, byte[]? rentedBuffer)
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
            ArrayPool<byte>.Shared.Return(RentedBuffer);
        }
    }
}