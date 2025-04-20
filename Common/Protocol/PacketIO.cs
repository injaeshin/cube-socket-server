using System.Buffers;
using System.Net.Sockets;
using Common.Transport;

namespace Common.Protocol;

public static class PacketIO
{
    private const int _headerSize = Constants.HEADER_SIZE;
    private const int _opcodeSize = Constants.OPCODE_SIZE;
    private const int _maxPacketSize = Constants.MAX_PACKET_SIZE;

    public static int GetHeaderSize() => _headerSize;

    public static SendRequest Build(string sessionId, Socket socket, PacketType type, ReadOnlyMemory<byte> payload)
    {
        ushort bodyLength = (ushort)(_opcodeSize + payload.Length);
        int totalLength = _headerSize + _opcodeSize + payload.Length;

        if (bodyLength > _maxPacketSize)
        {
            throw new InvalidOperationException($"Packet body length is too large: {bodyLength}");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(totalLength);
        var memory = buffer.AsMemory(0, totalLength);
        
        var span = memory.Span;
        // length (big endian)
        span[0] = (byte)(bodyLength >> 8);
        span[1] = (byte)(bodyLength & 0xFF);

        // opcode
        ushort opcode = (ushort)type;
        span[2] = (byte)(opcode >> 8);
        span[3] = (byte)(opcode & 0xFF);

        // payload
        payload.Span.CopyTo(span[(_headerSize + _opcodeSize)..]);

        return new SendRequest(sessionId, socket, memory, buffer);
    }

    public static bool TryParseHeader(ReadOnlyMemory<byte> memory, out ushort bodyLength)
    {
        bodyLength = 0;
        if (memory.Length < _headerSize)
        {
            return false;
        }

        var span = memory.Span;
        bodyLength = (ushort)(span[0] << 8 | span[1]);
        return bodyLength >= 2 && bodyLength <= _maxPacketSize;
    }

    public static PacketType GetPacketType(ReadOnlyMemory<byte> memory)
    {
        var span = memory.Span;
        ushort opcode = (ushort)(span[0] << 8 | span[1]);
        return (PacketType)opcode;
    }

    public static ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> memory)
    {
        return memory[_opcodeSize..];
    }
}
