using System.Buffers;
using Common.Network.Session;

namespace Common.Network.Packet;

public readonly struct ReceivedPacket(string sessionId, MessageType packetType, ReadOnlyMemory<byte> packet, byte[]? buffer, ISession? session = null)
{
    public string SessionId { get; } = sessionId;
    public MessageType Type { get; } = packetType;
    public ReadOnlyMemory<byte> Data { get; } = packet;
    public byte[]? Buffer { get; } = buffer;
    public ISession? Session { get; } = session;

    public void Return()
    {
        if (Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}