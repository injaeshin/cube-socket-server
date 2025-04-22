using System.Text;

namespace Common.Network.Packet;

public class PacketReader
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _pos;

    public PacketReader(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _pos = 0;
    }

    public ReadOnlyMemory<byte> RemainingMemory => _memory.Slice(_pos);

    public ReadOnlySpan<byte> RemainingBytes => _memory.Span.Slice(_pos);

    public bool IsEmpty => _pos >= _memory.Length;

    public int Position => _pos;

    public int RemainingLength => _memory.Length - _pos;

    private void EnsureRemaining(int count)
    {
        if (_pos + count > _memory.Length)
            throw new InvalidOperationException($"Buffer underflow: Need {count} bytes, but only {_memory.Length - _pos} available");
    }

    private ReadOnlySpan<byte> GetCurrentSpan(int length)
    {
        EnsureRemaining(length);
        return _memory.Span.Slice(_pos, length);
    }

    public byte ReadByte()
    {
        EnsureRemaining(1);
        return _memory.Span[_pos++];
    }

    public ushort ReadUInt16()
    {
        EnsureRemaining(2);
        ushort value = (ushort)(_memory.Span[_pos] << 8 | _memory.Span[_pos + 1]);
        _pos += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        var span = GetCurrentSpan(4);
        uint value = (uint)span[0] << 24 |
                    (uint)span[1] << 16 |
                    (uint)span[2] << 8 |
                    span[3];
        _pos += 4;
        return value;
    }

    public int ReadInt32()
    {
        var span = GetCurrentSpan(4);
        int value = span[0] << 24 |
                    span[1] << 16 |
                    span[2] << 8 |
                    span[3];
        _pos += 4;
        return value;
    }

    public long ReadInt64()
    {
        var span = GetCurrentSpan(8);
        long value = (long)span[0] << 56 |
                    (long)span[1] << 48 |
                    (long)span[2] << 40 |
                    (long)span[3] << 32 |
                    (long)span[4] << 24 |
                    (long)span[5] << 16 |
                    (long)span[6] << 8 |
                    span[7];
        _pos += 8;
        return value;
    }

    public float ReadSingle()
    {
        var span = GetCurrentSpan(4);
        uint bits = (uint)span[0] << 24 |
                    (uint)span[1] << 16 |
                    (uint)span[2] << 8 |
                    span[3];
        _pos += 4;
        return BitConverter.UInt32BitsToSingle(bits);
    }

    public double ReadDouble()
    {
        var span = GetCurrentSpan(8);
        ulong bits = (ulong)span[0] << 56 |
                    (ulong)span[1] << 48 |
                    (ulong)span[2] << 40 |
                    (ulong)span[3] << 32 |
                    (ulong)span[4] << 24 |
                    (ulong)span[5] << 16 |
                    (ulong)span[6] << 8 |
                    span[7];
        _pos += 8;
        return BitConverter.UInt64BitsToDouble(bits);
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        EnsureRemaining(length);
        var result = _memory.Span.Slice(_pos, length);
        _pos += length;
        return result;
    }

    public string ReadString()
    {
        ushort length = ReadUInt16();

        if (length == 0)
            return string.Empty;

        EnsureRemaining(length);

        string result = Encoding.UTF8.GetString(_memory.Span.Slice(_pos, length));
        _pos += length;
        return result;
    }

    public T[] ReadArray<T>(Func<T> readItem)
    {
        ushort length = ReadUInt16();

        if (length == 0)
            return Array.Empty<T>();

        T[] result = new T[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = readItem();
        }

        return result;
    }

    public void Skip(int count)
    {
        EnsureRemaining(count);
        _pos += count;
    }

    public void SkipSafe(int count)
    {
        _pos = Math.Min(_pos + count, _memory.Length);
    }

    public PacketType ReadPacketType()
    {
        return (PacketType)ReadUInt16();
    }

    public void Reset()
    {
        _pos = 0;
    }

    public void Seek(int position)
    {
        if (position < 0 || position > _memory.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _pos = position;
    }
}





