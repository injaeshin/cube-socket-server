using System.Buffers;
using System.Net.Sockets;

namespace Common.Transport;

public readonly struct SendRequest(string sessionId, Socket socket, ReadOnlyMemory<byte> packet, byte[]? buffer)
{
    public string SessionId { get; } = sessionId;
    public Socket Socket { get; } = socket;
    public ReadOnlyMemory<byte> Data { get; } = packet;
    public byte[]? Buffer { get; } = buffer;

    public void Return()
    {
        if (Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}