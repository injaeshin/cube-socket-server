using System.Text;

namespace Cube.Packet.Builder;

public sealed class PacketWriter : IDisposable
{
    /// <summary>
    /// 대여한 버퍼
    /// </summary>
    private readonly byte[] _buffer;
    private int _position;
    private const int UDP_HEADER_SIZE = PacketConsts.UDP_HEADER_SIZE; // RUDP 헤더 크기 (session_id, seq, ack)
    private const int LENGTH_SIZE = PacketConsts.LENGTH_SIZE; // 패킷 길이 크기 (TCP 헤더에서 사용)

    public PacketWriter(PacketType type, int size = PacketConsts.BUFFER_SIZE)
    {
        _buffer = BufferArrayPool.Rent(size);
        _position = UDP_HEADER_SIZE + LENGTH_SIZE;
        WriteUShort((ushort)type);
    }

    public int Length => _position - UDP_HEADER_SIZE;

    public PacketWriter WriteInt(int value)
    {
        _buffer[_position++] = (byte)(value >> 24);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    public PacketWriter WriteUShort(ushort value)
    {
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    public PacketWriter WriteShort(short value)
    {
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    public PacketWriter WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, _buffer, _position, bytes.Length);
        _position += bytes.Length;
        return this;
    }

    public PacketWriter WriteDouble(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, _buffer, _position, bytes.Length);
        _position += bytes.Length;
        return this;
    }

    public PacketWriter WriteBytes(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_buffer.AsSpan(_position));
        _position += data.Length;
        return this;
    }

    /// <summary>
    /// 문자열 쓰기 (UTF-8 인코딩, 길이 헤더 포함)
    /// </summary>
    public PacketWriter WriteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            // 길이 0 기록
            _buffer[_position++] = 0;
            _buffer[_position++] = 0;
            return this;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteUShort((ushort)bytes.Length);
        WriteBytes(bytes);
        return this;
    }

    // public void SetRudpHeader(string token, ushort seq, ushort ack)
    // {
    //     if (token.Length != PacketConsts.TOKEN_SIZE)
    //     {
    //         throw new ArgumentException("token 크기가 8바이트가 아닙니다.");
    //     }

    //     // token (8바이트)
    //     var tokenBytes = Encoding.UTF8.GetBytes(token);
    //     Buffer.BlockCopy(tokenBytes, 0, _buffer, 0, PacketConsts.TOKEN_SIZE);

    //     // seq (2바이트)
    //     _buffer[PacketConsts.TOKEN_SIZE] = (byte)(seq >> 8);
    //     _buffer[PacketConsts.TOKEN_SIZE + 1] = (byte)(seq & 0xFF);

    //     // ack (2바이트)
    //     _buffer[PacketConsts.TOKEN_SIZE + 2] = (byte)(ack >> 8);
    //     _buffer[PacketConsts.TOKEN_SIZE + 3] = (byte)(ack & 0xFF);
    // }

    public (Memory<byte> data, byte[]? rentedBuffer) ToTcpPacket()
    {
        // TCP 패킷: [길이(2)][타입(2)][데이터]
        var payloadLength = (ushort)(_position - UDP_HEADER_SIZE - LENGTH_SIZE);
        _buffer[UDP_HEADER_SIZE] = (byte)(payloadLength >> 8);
        _buffer[UDP_HEADER_SIZE + 1] = (byte)(payloadLength & 0xFF);
        var length = LENGTH_SIZE + payloadLength;
        return (new Memory<byte>(_buffer, UDP_HEADER_SIZE, length), _buffer);
    }

    public (Memory<byte> data, byte[]? rentedBuffer) ToUdpPacket()
    {
        // UDP 패킷: [토큰(8)][시퀀스(2)][ACK(2)][길이(2)][타입(2)][데이터]
        var payloadLength = (ushort)(_position - UDP_HEADER_SIZE - LENGTH_SIZE);
        _buffer[UDP_HEADER_SIZE] = (byte)(payloadLength >> 8);
        _buffer[UDP_HEADER_SIZE + 1] = (byte)(payloadLength & 0xFF);
        var length = UDP_HEADER_SIZE + LENGTH_SIZE + payloadLength;
        return (new Memory<byte>(_buffer, 0, length), _buffer);
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            BufferArrayPool.Return(_buffer);
        }
    }
}

