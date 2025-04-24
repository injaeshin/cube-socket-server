//using Common.Network.Packet;
//using System.Buffers;
//using System.Text;
//using Xunit;

//namespace Common.Tests.Network.Packet;

//public class StringPacketTests
//{
//    [Fact]
//    public void WriteAndReadString_SimpleString_Success()
//    {
//        // 준비
//        var testString = "Hello, World!";
        
//        // 쓰기
//        using var writer = new PacketWriter();
//        writer.Write(testString);
//        var packetData = writer.ToMemory();
        
//        // 읽기
//        var reader = new PacketReader(packetData);
//        var result = reader.ReadString();
        
//        // 검증
//        Assert.Equal(testString, result);
//    }
    
//    [Fact]
//    public void WriteAndReadString_EmptyString_Success()
//    {
//        // 준비
//        var testString = string.Empty;
        
//        // 쓰기
//        using var writer = new PacketWriter();
//        writer.Write(testString);
//        var packetData = writer.ToMemory();
        
//        // 읽기
//        var reader = new PacketReader(packetData);
//        var result = reader.ReadString();
        
//        // 검증
//        Assert.Equal(testString, result);
//    }
    
//    [Fact]
//    public void WriteAndReadString_Korean_Success()
//    {
//        // 준비
//        var testString = "안녕하세요, 세계!";
        
//        // 쓰기
//        using var writer = new PacketWriter();
//        writer.Write(testString);
//        var packetData = writer.ToMemory();
        
//        // 읽기
//        var reader = new PacketReader(packetData);
//        var result = reader.ReadString();
        
//        // 검증
//        Assert.Equal(testString, result);
//    }
    
//    [Fact]
//    public void WriteAndReadString_LongString_Success()
//    {
//        // 준비
//        var testString = new string('A', 1000);
        
//        // 쓰기
//        using var writer = new PacketWriter();
//        writer.Write(testString);
//        var packetData = writer.ToMemory();
        
//        // 읽기
//        var reader = new PacketReader(packetData);
//        var result = reader.ReadString();
        
//        // 검증
//        Assert.Equal(testString, result);
//    }
    
//    [Fact]
//    public void MultipleBytesAndStrings_MixedData_Success()
//    {
//        // 준비
//        var string1 = "Hello";
//        var number1 = (ushort)42;
//        var string2 = "World";
//        var number2 = (ushort)99;
        
//        // 쓰기
//        using var writer = new PacketWriter();
//        writer.Write(string1);
//        writer.Write(number1);
//        writer.Write(string2);
//        writer.Write(number2);
//        var packetData = writer.ToMemory();
        
//        // 읽기
//        var reader = new PacketReader(packetData);
//        var resultString1 = reader.ReadString();
//        var resultNumber1 = reader.ReadUInt16();
//        var resultString2 = reader.ReadString();
//        var resultNumber2 = reader.ReadUInt16();
        
//        // 검증
//        Assert.Equal(string1, resultString1);
//        Assert.Equal(number1, resultNumber1);
//        Assert.Equal(string2, resultString2);
//        Assert.Equal(number2, resultNumber2);
//    }
    
//    [Fact]
//    public void LoginPacket_Test()
//    {
//        // 준비
//        var username = "testuser";
        
//        // 1. 일반적인 로그인 패킷 생성
//        using var normalWriter = new PacketWriter();
//        normalWriter.Write((ushort)(2 + (2 + username.Length))); // 길이 (2) + username 문자열 헤더(2) + 내용(7)
//        normalWriter.Write((ushort)PacketType.Login);
//        normalWriter.Write(username);
//        var normalPacket = normalWriter.ToMemory();
        
//        // 패킷 버퍼에 추가
//        var buffer = new PacketBuffer();
//        buffer.TryAppend(normalPacket);
        
//        // 패킷 추출
//        bool result = buffer.TryReadPacket(out var packet, out var rentedBuffer);
        
//        // 검증
//        Assert.True(result);
//        try
//        {
//            // 패킷 타입 확인
//            var type = PacketIO.GetPacketType(packet);
//            Assert.Equal(PacketType.Login, type);
            
//            // 페이로드 확인
//            var payload = PacketIO.GetPayload(packet);
//            var reader = new PacketReader(payload);
//            var message = reader.ReadString();
//            Assert.Equal(username, message);
//        }
//        finally
//        {
//            if (rentedBuffer != null)
//                ArrayPool<byte>.Shared.Return(rentedBuffer);
//        }
//    }
    
//    [Fact]
//    public void BadStringLength_Test()
//    {
//        // 준비: 비정상적으로 큰 문자열 길이를 가진 패킷 생성
//        using var writer = new PacketWriter();
//        writer.Write((ushort)(2 + 2 + 5)); // 타입(2) + 문자열 헤더(2) + 5바이트 (실제 데이터는 2바이트만 있음)
//        writer.Write((ushort)PacketType.Login);
        
//        // 문자열 길이 필드만 비정상적으로 큰 값으로 설정 (30000)
//        writer.Write((ushort)30000); // 문자열 길이 (비정상적으로 큼)
        
//        var packetData = writer.ToMemory();
        
//        // 읽기 시도 - 예외 발생해야 함
//        var reader = new PacketReader(packetData.Slice(4)); // 헤더(길이 + 타입) 제외
//        Assert.Throws<InvalidOperationException>(() => reader.ReadString());
//    }
//} 