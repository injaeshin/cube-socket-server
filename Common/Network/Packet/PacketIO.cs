using System.Buffers;
using System.Net.Sockets;
using Common.Network.Transport;

namespace Common.Network.Packet;

// [ 2 bytes Length ][ 2 bytes Opcode ][ Payload... ]
//       ↑                ↑
//    ushort          PacketType (ushort enum)

public static class PacketIO
{
    private const int _lengthSize = Constant.LENGTH_SIZE;
    private const int _opcodeSize = Constant.OPCODE_SIZE;
    private const int _maxPacketSize = Constant.MAX_PACKET_SIZE;

    public static SendRequest Build(string sessionId, Socket socket, PacketType type, ReadOnlyMemory<byte> payload)
    {
        // 바디의 전체 크기 (opcode + payload)
        ushort bodyLength = (ushort)(_opcodeSize + payload.Length);
        // 패킷의 전체 크기 (헤더 + 바디)
        int totalLength = _lengthSize + bodyLength;

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
        payload.Span.CopyTo(span[(_lengthSize + _opcodeSize)..]);

        return new SendRequest(sessionId, socket, memory, buffer);
    }

    public static bool TryParseHeader(ReadOnlyMemory<byte> memory, out ushort bodyLength)
    {
        bodyLength = 0;
        if (memory.Length < _lengthSize)
        {
            return false;
        }

        var span = memory.Span;
        bodyLength = (ushort)(span[0] << 8 | span[1]);
        return bodyLength >= 2 && bodyLength <= _maxPacketSize;
    }

    /// <summary>
    /// 헤더가 제외된 패킷에서 패킷 타입을 반환합니다.
    /// </summary>
    /// <param name="memory">헤더가 제외된 패킷</param>
    /// <returns>패킷 타입</returns>
    public static PacketType GetPacketType(ReadOnlyMemory<byte> memory)
    {
        if (memory.Length < _opcodeSize)
        {
            throw new ArgumentException($"패킷 크기가 타입을 포함하기에 충분하지 않습니다. 필요: {_opcodeSize}, 실제: {memory.Length}");
        }

        var span = memory.Span;
        ushort opcode = (ushort)(span[0] << 8 | span[1]);
        
        // Enum 유효성 검사
        if (!Enum.IsDefined(typeof(PacketType), opcode) || opcode == 0 || opcode >= (ushort)PacketType.Max)
        {
            // 오류 대신 경고 로그만 남기고 값 반환 (비정상 패킷인 경우 상위 레벨에서 처리)
            System.Diagnostics.Debug.WriteLine($"[PacketIO] 알 수 없는 패킷 타입: 0x{opcode:X4}, 바이트: {span[0]:X2} {span[1]:X2}");
        }
        
        return (PacketType)opcode;
    }

    /// <summary>
    /// 헤더가 제외된 패킷에서 바디를 반환합니다.
    /// </summary>
    /// <param name="memory">헤더가 제외된 패킷</param>
    /// <returns>바디</returns>
    public static ReadOnlyMemory<byte> GetPayload(ReadOnlyMemory<byte> memory)
    {
        return memory[_opcodeSize..];
    }    
}
