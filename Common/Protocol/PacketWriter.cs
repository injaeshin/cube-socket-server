using System;
using System.Buffers;
using System.Text;

namespace Common.Protocol;

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
    /// 최대 사용 가능 크기
    /// </summary>
    private readonly int _maxSize;

    /// <summary>
    /// 현재 위치
    /// </summary>
    private int _position;

    /// <summary>
    /// 현재 위치
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// 남은 공간 바이트 수
    /// </summary>
    public int RemainingBytes => _maxSize - _position;

    /// <summary>
    /// 기본 크기로 패킷 작성기 생성
    /// </summary>
    public PacketWriter() : this(Constants.PACKET_BUFFER_SIZE)
    {
    }

    /// <summary>
    /// 지정된 크기로 패킷 작성기 생성
    /// </summary>
    /// <param name="size">버퍼 크기</param>
    public PacketWriter(int size)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(size);
        _maxSize = size;
        _position = 0;
    }

    public static PacketWriter Create()
    {
        return new PacketWriter();
    }

    /// <summary>
    /// 용량 확인 도우미 메서드
    /// </summary>
    private void EnsureCapacity(int count)
    {
        if (_position + count > _maxSize)
        {
            throw new InvalidOperationException($"버퍼 오버플로우: {count}바이트 필요하지만 {_maxSize - _position}바이트만 사용 가능");
        }
    }

    /// <summary>
    /// 바이트 값 쓰기
    /// </summary>
    public PacketWriter Write(byte value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value;
        return this;
    }

    /// <summary>
    /// 부호 없는 short 값 쓰기
    /// </summary>
    public PacketWriter Write(ushort value)
    {
        EnsureCapacity(2);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    /// <summary>
    /// 부호 없는 int 값 쓰기
    /// </summary>
    public PacketWriter Write(uint value)
    {
        EnsureCapacity(4);
        _buffer[_position++] = (byte)(value >> 24);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    /// <summary>
    /// int 값 쓰기
    /// </summary>
    public PacketWriter Write(int value)
    {
        EnsureCapacity(4);
        _buffer[_position++] = (byte)(value >> 24);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    /// <summary>
    /// long 값 쓰기
    /// </summary>
    public PacketWriter Write(long value)
    {
        EnsureCapacity(8);
        _buffer[_position++] = (byte)(value >> 56);
        _buffer[_position++] = (byte)(value >> 48);
        _buffer[_position++] = (byte)(value >> 40);
        _buffer[_position++] = (byte)(value >> 32);
        _buffer[_position++] = (byte)(value >> 24);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value & 0xFF);
        return this;
    }

    /// <summary>
    /// float 값 쓰기
    /// </summary>
    public PacketWriter Write(float value)
    {
        EnsureCapacity(4);
        uint bits = BitConverter.SingleToUInt32Bits(value);
        _buffer[_position++] = (byte)(bits >> 24);
        _buffer[_position++] = (byte)(bits >> 16);
        _buffer[_position++] = (byte)(bits >> 8);
        _buffer[_position++] = (byte)(bits & 0xFF);
        return this;
    }

    /// <summary>
    /// double 값 쓰기
    /// </summary>
    public PacketWriter Write(double value)
    {
        EnsureCapacity(8);
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        _buffer[_position++] = (byte)(bits >> 56);
        _buffer[_position++] = (byte)(bits >> 48);
        _buffer[_position++] = (byte)(bits >> 40);
        _buffer[_position++] = (byte)(bits >> 32);
        _buffer[_position++] = (byte)(bits >> 24);
        _buffer[_position++] = (byte)(bits >> 16);
        _buffer[_position++] = (byte)(bits >> 8);
        _buffer[_position++] = (byte)(bits & 0xFF);
        return this;
    }

    /// <summary>
    /// 바이트 배열 쓰기
    /// </summary>
    public PacketWriter Write(ReadOnlySpan<byte> value)
    {
        EnsureCapacity(value.Length);
        value.CopyTo(new Span<byte>(_buffer, _position, value.Length));
        _position += value.Length;
        return this;
    }

    /// <summary>
    /// 문자열 쓰기 (UTF-8 인코딩, 길이 헤더 포함)
    /// </summary>
    public PacketWriter Write(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            // 빈 문자열 처리
            Write((ushort)0);
            return this;
        }

        // UTF-8 인코딩 바이트 수 계산
        int byteCount = Encoding.UTF8.GetByteCount(value);

        // 문자열 길이가 ushort 범위를 초과하는지 확인
        if (byteCount > ushort.MaxValue)
            throw new ArgumentException($"문자열이 너무 김: {byteCount}바이트가 최대 {ushort.MaxValue}바이트를 초과함");

        // 필요한 공간 확인 (길이 + 내용)
        EnsureCapacity(2 + byteCount);

        // 길이 기록
        Write((ushort)byteCount);

        // 할당 없이 직접 버퍼에 인코딩
        Encoding.UTF8.GetBytes(value, new Span<byte>(_buffer, _position, byteCount));
        _position += byteCount;
        
        return this;
    }

    /// <summary>
    /// 배열 쓰기 (길이 헤더 포함)
    /// </summary>
    public PacketWriter WriteArray<T>(T[] array, Action<PacketWriter, T> writeItem) where T : notnull
    {
        if (array == null || array.Length == 0)
        {
            Write((ushort)0);
            return this;
        }

        if (array.Length > ushort.MaxValue)
            throw new ArgumentException($"배열이 너무 큼: {array.Length}항목이 최대 {ushort.MaxValue}항목을 초과함");

        // 길이 쓰기
        Write((ushort)array.Length);

        // 각 아이템 쓰기
        foreach (var item in array)
        {
            writeItem(this, item);
        }
        
        return this;
    }

    // /// <summary>
    // /// 작성기 초기화 및 재사용 준비
    // /// </summary>
    // public void Reset()
    // {
    //     _position = 0;
    // }

    /// <summary>
    /// 현재까지 작성된 데이터를 ReadOnlyMemory로 반환
    /// </summary>
    public ReadOnlyMemory<byte> ToMemory()
    {
        return new ReadOnlyMemory<byte>(_buffer, 0, _position);
    }

    /// <summary>
    /// 리소스 해제 및 버퍼 반환
    /// </summary>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}

