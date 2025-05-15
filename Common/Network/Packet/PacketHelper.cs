using System.Buffers;
using Common.Network.Session;

namespace Common.Network.Packet;

public readonly struct ReceivedPacket(MessageType packetType, ReadOnlyMemory<byte> packet, byte[]? buffer, INetSession session)
{
    public string SessionId { get; } = session.SessionId;
    public MessageType Type { get; } = packetType;
    public ReadOnlyMemory<byte> Data { get; } = packet;
    public byte[]? Buffer { get; } = buffer;
    public INetSession Session { get; } = session;

    public void Return()
    {
        if (Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}