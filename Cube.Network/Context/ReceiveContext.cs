using System.Buffers;

namespace Cube.Network.Context;

public class ReceivedContext
{
    public string SessionId { get; init; }
    public ReadOnlyMemory<byte> Data { get; init; }
    public byte[]? Buffer { get; init; }

    public ReceivedContext(string sessionId, ReadOnlyMemory<byte> data, byte[]? buffer)
    {
        SessionId = sessionId;
        Data = data;
        Buffer = buffer;
    }

    public void Return()
    {
        if (Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}