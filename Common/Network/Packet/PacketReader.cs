using System.Text;

namespace Common.Network.Packet;

public ref struct PacketReader
{
    private ReadOnlySpan<byte> _span;
    private int _pos;

    public PacketReader(ReadOnlySpan<byte> span)
    {
        _span = span;
        _pos = 0;
    }

    public ReadOnlySpan<byte> RemainingBytes => _span.Slice(_pos);

    public bool IsEmpty => _pos >= _span.Length;

    // 현재 위치 속성 추가
    public int Position => _pos;

    // 남은 바이트 수 속성
    public int RemainingLength => _span.Length - _pos;

    // 용량 확인 도우미 메서드
    private void EnsureRemaining(int count)
    {
        if (_pos + count > _span.Length)
            throw new InvalidOperationException($"Buffer underflow: Need {count} bytes, but only {_span.Length - _pos} available");
    }

    public byte ReadByte()
    {
        EnsureRemaining(1);
        return _span[_pos++];
    }

    public ushort ReadUInt16()
    {
        EnsureRemaining(2);
        ushort value = (ushort)(_span[_pos] << 8 | _span[_pos + 1]);
        _pos += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        EnsureRemaining(4);
        uint value = (uint)_span[_pos] << 24 |
                    (uint)_span[_pos + 1] << 16 |
                    (uint)_span[_pos + 2] << 8 |
                    _span[_pos + 3];
        _pos += 4;
        return value;
    }

    public int ReadInt32()
    {
        EnsureRemaining(4);
        int value = _span[_pos] << 24 |
                    _span[_pos + 1] << 16 |
                    _span[_pos + 2] << 8 |
                    _span[_pos + 3];
        _pos += 4;
        return value;
    }

    public long ReadInt64()
    {
        EnsureRemaining(8);
        long value = (long)_span[_pos] << 56 |
                    (long)_span[_pos + 1] << 48 |
                    (long)_span[_pos + 2] << 40 |
                    (long)_span[_pos + 3] << 32 |
                    (long)_span[_pos + 4] << 24 |
                    (long)_span[_pos + 5] << 16 |
                    (long)_span[_pos + 6] << 8 |
                    _span[_pos + 7];
        _pos += 8;
        return value;
    }

    public float ReadSingle()
    {
        EnsureRemaining(4);
        uint bits = (uint)_span[_pos] << 24 |
                    (uint)_span[_pos + 1] << 16 |
                    (uint)_span[_pos + 2] << 8 |
                    _span[_pos + 3];
        _pos += 4;
        return BitConverter.UInt32BitsToSingle(bits);
    }

    public double ReadDouble()
    {
        EnsureRemaining(8);
        ulong bits = (ulong)_span[_pos] << 56 |
                    (ulong)_span[_pos + 1] << 48 |
                    (ulong)_span[_pos + 2] << 40 |
                    (ulong)_span[_pos + 3] << 32 |
                    (ulong)_span[_pos + 4] << 24 |
                    (ulong)_span[_pos + 5] << 16 |
                    (ulong)_span[_pos + 6] << 8 |
                    _span[_pos + 7];
        _pos += 8;
        return BitConverter.UInt64BitsToDouble(bits);
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        EnsureRemaining(length);
        var result = _span.Slice(_pos, length);
        _pos += length;
        return result;
    }

    public string ReadString()
    {
        // 문자열 길이 읽기
        ushort length = ReadUInt16();

        // 길이가 0이면 빈 문자열 반환
        if (length == 0)
            return string.Empty;

        EnsureRemaining(length);

        // 문자열 디코딩
        string result = Encoding.UTF8.GetString(_span.Slice(_pos, length));
        _pos += length;
        return result;
    }

    // 배열 읽기 (길이 헤더 포함)
    public T[] ReadArray<T>(Func<T> readItem)
    {
        // 배열 길이 읽기
        ushort length = ReadUInt16();

        // 길이가 0이면 빈 배열 반환
        if (length == 0)
            return Array.Empty<T>();

        // 배열 생성 및 채우기
        T[] result = new T[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = readItem();
        }

        return result;
    }

    // 지정된 길이만큼 건너뛰기
    public void Skip(int count)
    {
        EnsureRemaining(count);
        _pos += count;
    }

    // 다음 위치로 이동하되, 경계를 넘어가지 않도록 함
    public void SkipSafe(int count)
    {
        _pos = Math.Min(_pos + count, _span.Length);
    }

    // 특정 패킷 타입을 읽음
    public PacketType ReadPacketType()
    {
        return (PacketType)ReadUInt16();
    }

    // 현재 위치 재설정
    public void Reset()
    {
        _pos = 0;
    }

    // 특정 위치로 이동
    public void Seek(int position)
    {
        if (position < 0 || position > _span.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _pos = position;
    }
}





