using System.Buffers;
using Server.Core.Session;

namespace Server.Core.Packet;

public readonly struct ReceivedPacket
{
    public string SessionId { get; }
    public ReadOnlyMemory<byte> Data { get; }
    public byte[]? Buffer { get; }
    public ISocketSession? Session { get; }

    public ReceivedPacket(string sessionId, ReadOnlyMemory<byte> packet, byte[]? buffer, ISocketSession? session = null)
    {
        SessionId = sessionId;
        Data = packet;
        Buffer = buffer;
        Session = session;
    }

    public void Return()
    {
        if (Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}