using System.Buffers;

namespace Common.Network.Packet;

public class PacketBuffer
{
    private readonly byte[] _buffer = new byte[Constant.PACKET_BUFFER_SIZE];

    private readonly int _bufferSize = Constant.PACKET_BUFFER_SIZE;
    private readonly int _headerSize = Constant.HEADER_SIZE;

    private int _readPos = 0;
    private int _writePos = 0;

    public bool Append(ReadOnlySpan<byte> data)
    {
        if (FreeSize() < data.Length)
            return false;

        int firstCopyLength = Math.Min(data.Length, _bufferSize - _writePos);
        data[..firstCopyLength].CopyTo(_buffer.AsSpan(_writePos));

        int remainLength = data.Length - firstCopyLength;
        if (remainLength > 0)
            data[firstCopyLength..].CopyTo(_buffer.AsSpan(0));

        _writePos = (_writePos + data.Length) % _bufferSize;
        return true;
    }

    public bool Append(ReadOnlyMemory<byte> data)
    {
        return Append(data.Span);
    }

    public bool TryReadPacket(out ReadOnlyMemory<byte> packet, out byte[]? rentedBuffer)
    {
        packet = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (!TryPeekUShort(out ushort length))
            return false;

        if (length == 0 || length > _bufferSize - _headerSize)
            return false;

        if (DataSize() < _headerSize + length)
            return false;

        Skip(_headerSize);

        rentedBuffer = ArrayPool<byte>.Shared.Rent(length);

        if (_bufferSize - _readPos >= length)
        {
            _buffer.AsSpan(_readPos, length).CopyTo(rentedBuffer);
        }
        else
        {
            int firstLen = _bufferSize - _readPos;
            _buffer.AsSpan(_readPos, firstLen).CopyTo(rentedBuffer);
            _buffer.AsSpan(0, length - firstLen).CopyTo(rentedBuffer.AsSpan(firstLen));
        }

        packet = new ReadOnlyMemory<byte>(rentedBuffer, 0, length);
        Skip(length);

        return true;
    }

    // private bool TryPeekHeader(out ushort length)
    // {
    //     length = 0;

    //     if (DataSize() < _headerSize)
    //         return false;

    //     length = ReadUInt16FromBuffer(0);

    //     return true;
    // }

    private ushort ReadUInt16FromBuffer(int offset)
    {
        if (_bufferSize - (_readPos + offset) >= 2)
        {
            // 버퍼 경계를 넘지 않는 경우
            return (ushort)(_buffer[_readPos + offset] << 8 | _buffer[_readPos + offset + 1]);
        }
        else
        {
            // 버퍼 경계에 걸친 경우
            byte high = _buffer[(_readPos + offset) % _bufferSize];
            byte low = _buffer[(_readPos + offset + 1) % _bufferSize];
            return (ushort)(high << 8 | low);
        }
    }

    private bool TryPeekUShort(out ushort value)
    {
        value = 0;

        if (DataSize() < 2) // 최소한 길이 필드만큼은 있어야 함
            return false;

        value = ReadUInt16FromBuffer(0);
        return true;
    }

    private void Skip(int count)
    {
        _readPos = (_readPos + count) % _bufferSize;
    }

    private int FreeSize()
    {
        if (_writePos >= _readPos)
            return _bufferSize - (_writePos - _readPos) - 1;

        return _readPos - _writePos - 1;
    }

    private int DataSize()
    {
        if (_writePos >= _readPos)
            return _writePos - _readPos;

        return _bufferSize - _readPos + _writePos;
    }

    public void Reset()
    {
        _readPos = 0;
        _writePos = 0;
    }
}

