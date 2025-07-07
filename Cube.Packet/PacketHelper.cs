using System.Text;

namespace Cube.Packet;

public static class PacketHelper
{
    public static (Memory<byte> data, byte[]? rentedBuffer) CopyTo(ReadOnlyMemory<byte> data)
    {
        if (!BufferArrayPool.TryRent(data.Length, out var rentedBuffer))
        {
            throw new Exception("Failed to rent buffer");
        }

        data.Span.CopyTo(rentedBuffer.AsSpan(0, data.Length));
        return (new Memory<byte>(rentedBuffer, 0, data.Length), rentedBuffer);
    }

    public static bool IsValidatePacketType(PacketType packetType)
    {
        return Enum.IsDefined(typeof(PacketType), packetType);
    }

    public static bool SetUdpHeader(this Memory<byte> data, string sessionId, ushort seq)
    {
        if (data.Length < PacketConsts.UDP_HEADER_SIZE)
        {
            return false;
        }

        var span = data.Span;

        // sessionId 복사
        Encoding.UTF8.GetBytes(sessionId, span.Slice(0, PacketConsts.TOKEN_SIZE));

        // seq, 설정
        span[PacketConsts.TOKEN_SIZE] = (byte)(seq >> 8);
        span[PacketConsts.TOKEN_SIZE + 1] = (byte)(seq & 0xFF);

        return true;
    }

    public static bool SetUdpAckHeader(this Memory<byte> data, string sessionId, ushort ack)
    {
        if (data.Length < PacketConsts.UDP_HEADER_SIZE)
        {
            return false;
        }

        var span = data.Span;

        // sessionId 복사
        Encoding.UTF8.GetBytes(sessionId, span.Slice(0, PacketConsts.TOKEN_SIZE));

        // ack 설정
        span[PacketConsts.TOKEN_SIZE + 2] = (byte)(ack >> 8);
        span[PacketConsts.TOKEN_SIZE + 3] = (byte)(ack & 0xFF);

        return true;
    }

    public static string ToHexDump(this ReadOnlyMemory<byte> data)
    {
        return BitConverter.ToString(data.ToArray()).Replace("-", " ");
    }
}

public static class Extensions
{
    public static bool TryGetValidateUdpPacket(this ReadOnlyMemory<byte> data,
                                        out string token,
                                        out ushort sequence,
                                        out ushort ack,
                                        out PacketType packetType,
                                        out ReadOnlyMemory<byte> payload,
                                        out byte[]? rentedBuffer)
    {
        token = string.Empty;
        sequence = 0;
        ack = 0;
        packetType = PacketType.None;
        payload = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        // 1. 헤더 검증
        if (data.Length < 16) // token(8) + sequence(2) + ack(2) + length(2) + packetType(2)
        {
            return false;
        }

        try
        {
            // 2. 헤더 파싱
            var span = data.Span;
            token = Encoding.UTF8.GetString(span[..8]);
            sequence = (ushort)(span[8] << 8 | span[9]);
            ack = (ushort)(span[10] << 8 | span[11]);

            // 3. 헤더를 제외한 크기 검증
            ushort totalLength = (ushort)(span[12] << 8 | span[13]);
            if (totalLength < PacketConsts.TYPE_SIZE)
            {
                return false;
            }

            // 4. 패킷 타입
            packetType = (PacketType)(span[14] << 8 | span[15]);
            if (!PacketHelper.IsValidatePacketType(packetType))
            {
                return false;
            }

            if (totalLength - PacketConsts.TYPE_SIZE > 0)
            {
                if (!BufferArrayPool.TryRent(totalLength - PacketConsts.TYPE_SIZE, out rentedBuffer))
                {
                    return false;
                }

                // copy to rented buffer (패킷 타입 제외)
                span[16..].CopyTo(rentedBuffer.AsSpan(0, totalLength - PacketConsts.TYPE_SIZE));
                payload = new ReadOnlyMemory<byte>(rentedBuffer, 0, totalLength - PacketConsts.TYPE_SIZE);
            }
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}