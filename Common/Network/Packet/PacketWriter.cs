using System;
using System.Buffers;
using System.Text;

namespace Common.Network.Packet;

/// <summary>
/// 패킷 페이로드 작성을 위한 클래스
/// ArrayPool에서 버퍼를 대여하고 IDisposable을 구현하여 자원을 관리합니다.
/// </summary>
public sealed class PacketWriter : IDisposable
{
    /// <summary>
    /// 대여한 버퍼
    /// </summary>
    private readonly byte[] _buffer;

    /// <summary>
    /// 현재 위치
    /// </summary>
    private int _position;

    public PacketWriter(MessageType type) : this()
    {
        WriteType(type);
    }

    /// <summary>
    /// 기본 크기로 패킷 작성기 생성
    /// </summary>
    public PacketWriter(int size = 2048)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(size);
        _position = 0;
    }

    /// <summary>
    /// 패킷 타입 쓰기
    /// </summary>
    public PacketWriter WriteType(MessageType type)
    {
        _buffer[_position++] = (byte)((ushort)type >> 8);
        _buffer[_position++] = (byte)((ushort)type & 0xFF);
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

    /// <summary>
    /// 현재까지 작성된 데이터를 ReadOnlyMemory로 반환
    /// [2바이트 Length][Payload...]
    /// </summary>
    public ReadOnlyMemory<byte> ToPacket()
    {
        // Length = Type(2) + Message
        ushort length = (ushort)_position;
        var packet = new byte[2 + length];
        packet[0] = (byte)(length >> 8);
        packet[1] = (byte)(length & 0xFF);
        Array.Copy(_buffer, 0, packet, 2, length);
        return packet;
    }

    /// <summary>
    /// 리소스 해제 및 버퍼 반환
    /// </summary>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}

