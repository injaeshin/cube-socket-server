using Common.Network.Packet;
using System.Buffers;

namespace Common.Tests.Network.Packet;

public class PacketBufferTests
{
    [Fact]
    public void TryReadPacket_SinglePacket_Success()
    {
        // 준비
        var buffer = new PacketBuffer();

        // 패킷 데이터 생성: [길이(2)][타입(2)][페이로드(문자열 길이(2) + 내용(5))]
        using var writer = new PacketWriter();
        writer.Write((ushort)(2 + 2 + 5)); // 타입(2) + 문자열 길이(2) + 문자열 내용(5) 길이
        writer.Write((ushort)PacketType.Login);
        writer.Write("login"); // 페이로드 (문자열 헤더 2바이트 + 내용 5바이트)

        var packetData = writer.ToMemory();

        // 실행
        buffer.TryAppend(packetData);
        bool result = buffer.TryReadPacket(out var packet, out var rentedBuffer);

        // 검증
        Assert.True(result);
        Assert.Equal(9, packet.Length); // 타입(2) + 문자열 길이(2) + 문자열 내용(5)

        // 패킷 타입 확인
        var type = PacketIO.GetPacketType(packet);
        Assert.Equal(PacketType.Login, type);

        // 패킷 페이로드 확인
        var payload = PacketIO.GetPayload(packet);
        var reader = new PacketReader(payload);
        var message = reader.ReadString();
        Assert.Equal("login", message);

        // 임대 버퍼 반환
        if (rentedBuffer != null)
            ArrayPool<byte>.Shared.Return(rentedBuffer);
    }

    [Fact]
    public void TryReadPacket_MultiplePackets_Success()
    {
        // 준비
        var buffer = new PacketBuffer();

        // 첫 번째 패킷
        using var writer1 = new PacketWriter();
        writer1.Write((ushort)(2 + 2 + 5)); // 타입(2) + 문자열 길이(2) + 문자열 내용(5) 길이
        writer1.Write((ushort)PacketType.Pong);
        writer1.Write("ping1"); // 페이로드
        var packet1 = writer1.ToMemory();

        // 두 번째 패킷
        using var writer2 = new PacketWriter();
        writer2.Write((ushort)(2 + 2 + 5)); // 타입(2) + 문자열 길이(2) + 문자열 내용(5) 길이
        writer2.Write((ushort)PacketType.Login);
        writer2.Write("login"); // 페이로드
        var packet2 = writer2.ToMemory();

        // 단일 버퍼에 두 패킷 추가
        using var combinedWriter = new PacketWriter();
        combinedWriter.Write(packet1.Span);
        combinedWriter.Write(packet2.Span);
        var combinedPackets = combinedWriter.ToMemory();

        // 실행
        buffer.TryAppend(combinedPackets);

        // 첫 번째 패킷 읽기
        bool result1 = buffer.TryReadPacket(out var readPacket1, out var rentedBuffer1);
        Assert.True(result1);

        try
        {
            var type1 = PacketIO.GetPacketType(readPacket1);
            Assert.Equal(PacketType.Pong, type1);
        }
        finally
        {
            if (rentedBuffer1 != null)
                ArrayPool<byte>.Shared.Return(rentedBuffer1);
        }

        // 두 번째 패킷 읽기
        bool result2 = buffer.TryReadPacket(out var readPacket2, out var rentedBuffer2);
        Assert.True(result2);

        try
        {
            var type2 = PacketIO.GetPacketType(readPacket2);
            Assert.Equal(PacketType.Login, type2);
        }
        finally
        {
            if (rentedBuffer2 != null)
                ArrayPool<byte>.Shared.Return(rentedBuffer2);
        }

        // 더 이상 패킷이 없어야 함
        bool result3 = buffer.TryReadPacket(out _, out var rentedBuffer3);
        Assert.False(result3);
        if (rentedBuffer3 != null)
            ArrayPool<byte>.Shared.Return(rentedBuffer3);
    }
}