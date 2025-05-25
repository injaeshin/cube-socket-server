using System.Buffers;
using System.Text;

namespace Cube.Network.PacketIO;

/// <summary>
/// 패킷 페이로드 작성을 위한 클래스
/// 사용자가 직접 버퍼를 관리해야 합니다.
/// </summary>
public sealed class PacketWriter
{
    /// <summary>
    /// 대여한 버퍼
    /// </summary>
    private readonly byte[] _buffer;
    private int _position;
    private const int RUD_HEADER_SIZE = 4;// rudp header 4byte (seq 2byte + ack 2byte)
    private const int LENGTH_SIZE = 2;

    public PacketWriter(ushort type) : this()
    {
        WriteType(type);
    }

    public int Length => _position - RUD_HEADER_SIZE;

    /// <summary>
    /// 기본 크기로 패킷 작성기 생성
    /// </summary>
    public PacketWriter(int size = 2048)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(size);
        _position = RUD_HEADER_SIZE + LENGTH_SIZE;
    }

    /// <summary>
    /// 패킷 타입 쓰기
    /// </summary>
    public PacketWriter WriteType(ushort type)
    {
        _buffer[_position++] = (byte)((ushort)type >> 8);
        _buffer[_position++] = (byte)((ushort)type & 0xFF);
        return this;
    }

    public PacketWriter WriteInt(int value)
    {
        _buffer[_position++] = (byte)(value >> 24);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    /// <summary>
    /// 바이트 배열 쓰기
    /// </summary>
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
        // 문자열 길이(ushort) 기록
        _buffer[_position++] = (byte)(bytes.Length >> 8);
        _buffer[_position++] = (byte)(bytes.Length & 0xFF);
        WriteBytes(bytes);
        return this;
    }

    public void SetRudpHeader(ushort seq, ushort ack)
    {
        _buffer[0] = (byte)(seq >> 8);
        _buffer[1] = (byte)(seq & 0xFF);
        _buffer[2] = (byte)(ack >> 8);
        _buffer[3] = (byte)(ack & 0xFF);
    }

    public (Memory<byte> data, byte[]? rentedBuffer) ToTcpPacket()
    {
        var length = FillLength();
        return (new Memory<byte>(_buffer, RUD_HEADER_SIZE, length), _buffer);
    }

    public (Memory<byte> data, byte[]? rentedBuffer) ToUdpPacket()
    {
        var length = FillLength();
        return (new Memory<byte>(_buffer, 0, length + RUD_HEADER_SIZE), _buffer);
    }

    private int FillLength()
    {
        ushort length = (ushort)(_position - RUD_HEADER_SIZE - LENGTH_SIZE);
        _buffer[RUD_HEADER_SIZE] = (byte)(length >> 8);
        _buffer[RUD_HEADER_SIZE + 1] = (byte)(length & 0xFF);
        return _position - RUD_HEADER_SIZE;
    }
}

