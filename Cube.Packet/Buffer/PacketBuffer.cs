using System.Text;

namespace Cube.Packet;

public class PacketBuffer : IDisposable
{
    private int _bufferSize = 0;
    private Memory<byte> _buffer;

    private int _readPos = 0;
    private int _writePos = 0;

    public Memory<byte> Memory => _buffer;

    public void Initialize(Memory<byte> buffer)
    {
        _buffer = buffer;
        _bufferSize = buffer.Length;
    }

    public void Clear()
    {
        _buffer = Memory<byte>.Empty;
        _bufferSize = 0;
        _readPos = 0;
        _writePos = 0;
    }

    public bool TryAppend(ReadOnlySpan<byte> data)
    {
        if (_bufferSize == 0) return false;

        int freeSize = FreeSize();
        if (freeSize < data.Length) return false;

        int tailSpace = _bufferSize - _writePos;
        if (data.Length <= tailSpace)
        {
            // 한 번에 복사 가능
            data.CopyTo(_buffer.Span[_writePos..]);
        }
        else
        {
            // 두 번에 나눠서 복사
            data[..tailSpace].CopyTo(_buffer.Span[_writePos..]);
            data[tailSpace..].CopyTo(_buffer.Span[..(data.Length - tailSpace)]);
        }

        _writePos = (_writePos + data.Length) % _bufferSize;
        return true;
    }

    public bool TryAppend(Memory<byte> data, int writtenBytes)
    {
        if (_bufferSize == 0) return false;

        int freeSize = FreeSize();
        if (freeSize < writtenBytes) return false;

        int tailSpace = _bufferSize - _writePos;
        if (writtenBytes <= tailSpace)
        {
            data[..writtenBytes].Span.CopyTo(_buffer.Span[_writePos..]);
        }
        else
        {
            data[..tailSpace].Span.CopyTo(_buffer.Span[_writePos..]);
            data[tailSpace..writtenBytes].Span.CopyTo(_buffer.Span[..(writtenBytes - tailSpace)]);
        }

        _writePos = (_writePos + writtenBytes) % _bufferSize;
        return true;
    }

    public bool TryAppend(ReadOnlyMemory<byte> data)
    {
        return TryAppend(data.Span);
    }

    public bool TryGetValidatePacket(out PacketType packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    {
        packetType = PacketType.None;
        payload = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (_bufferSize == 0 || DataSize() < PacketConsts.LENGTH_SIZE)
            return false;

        ushort bodyLength = ReadUInt16FromBuffer(0);
        if (bodyLength < PacketConsts.TYPE_SIZE || bodyLength > PacketConsts.BUFFER_SIZE - PacketConsts.TCP_HEADER_SIZE)
            return false;

        int totalPacketSize = PacketConsts.LENGTH_SIZE + bodyLength;  // 길이 + (타입 + 페이로드)
        if (DataSize() < totalPacketSize) return false;

        // 타입 읽기
        packetType = (PacketType)ReadUInt16FromBuffer(PacketConsts.TYPE_SIZE);
        if (!PacketHelper.IsValidatePacketType(packetType))
            return false;

        // 페이로드 크기 (타입 제외)
        int payloadLength = bodyLength - PacketConsts.TYPE_SIZE;
        if (!BufferArrayPool.TryRent(payloadLength, out rentedBuffer))
            return false;

        // 페이로드 시작 위치 (길이 + 타입 다음부터)
        int payloadStart = (_readPos + PacketConsts.LENGTH_SIZE + PacketConsts.TYPE_SIZE) % _bufferSize;
        if (_bufferSize - payloadStart >= payloadLength)
        {
            _buffer.Span[payloadStart..(payloadStart + payloadLength)].CopyTo(rentedBuffer.AsSpan(0, payloadLength));
        }
        else
        {
            int firstPart = _bufferSize - payloadStart;
            _buffer.Span[payloadStart..].CopyTo(rentedBuffer.AsSpan(0, firstPart));
            _buffer.Span[..(payloadLength - firstPart)].CopyTo(rentedBuffer.AsSpan(firstPart, payloadLength - firstPart));
        }

        payload = new ReadOnlyMemory<byte>(rentedBuffer, 0, payloadLength);  // 페이로드만
        Skip(totalPacketSize);
        return true;
    }

    public bool TryGetValidateUdpPacket(out string token, out ushort sequence, out ushort ack, out PacketType packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    {
        token = string.Empty;
        sequence = 0;
        ack = 0;
        packetType = PacketType.None;
        payload = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (_bufferSize == 0 || DataSize() < PacketConsts.UDP_HEADER_SIZE + PacketConsts.TCP_HEADER_SIZE)
            return false;

        token = ReadStringFromBuffer(PacketConsts.TOKEN_SIZE);
        sequence = ReadUInt16FromBuffer(PacketConsts.TOKEN_SIZE + PacketConsts.SEQUENCE_SIZE);
        ack = ReadUInt16FromBuffer(PacketConsts.TOKEN_SIZE + PacketConsts.SEQUENCE_SIZE + PacketConsts.ACK_SIZE);

        Skip(PacketConsts.UDP_HEADER_SIZE);
        return TryGetValidatePacket(out packetType, out payload, out rentedBuffer);
    }

    private string ReadStringFromBuffer(int offset)
    {
        int realOffset = (_readPos + offset) % _bufferSize;
        int remainingToEnd = _bufferSize - realOffset;

        if (offset <= remainingToEnd)
        {
            // 데이터가 끝을 넘어가지 않는 경우
            return Encoding.UTF8.GetString(_buffer.Span[realOffset..(realOffset + offset)]);
        }
        else
        {
            // 데이터가 끝을 넘어서 시작 부분까지 이어지는 경우
            if (!BufferArrayPool.TryRent(offset, out var tempBuffer))
                return string.Empty;

            // 끝 부분 데이터 복사
            _buffer.Span[realOffset..].CopyTo(tempBuffer);
            // 시작 부분 데이터 복사
            _buffer.Span[..(offset - remainingToEnd)].CopyTo(tempBuffer.AsSpan(remainingToEnd));
            var result = Encoding.UTF8.GetString(tempBuffer!);
            BufferArrayPool.Return(tempBuffer);
            return result;
        }
    }

    private ushort ReadUInt16FromBuffer(int offset)
    {
        int realOffset = (_readPos + offset) % _bufferSize;
        int remainingToEnd = _bufferSize - realOffset;

        if (offset <= remainingToEnd)
        {
            // 데이터가 끝을 넘어가지 않는 경우
            return (ushort)(_buffer.Span[realOffset] << 8 | _buffer.Span[realOffset + 1]);
        }
        else
        {
            // 데이터가 끝을 넘어서 시작 부분까지 이어지는 경우
            return (ushort)(_buffer.Span[realOffset] << 8 | _buffer.Span[0]);
        }
    }

    public void Skip(int count)
    {
        _readPos = (_readPos + count) % _bufferSize;
    }

    public int FreeSize()
    {
        if (_writePos >= _readPos)
        {
            return _bufferSize - (_writePos - _readPos) - 1;
        }

        return _readPos - _writePos - 1;
    }

    public int DataSize()
    {
        if (_writePos >= _readPos)
        {
            return _writePos - _readPos;
        }

        return _bufferSize - _readPos + _writePos;
    }

    public void Reset()
    {
        _readPos = 0;
        _writePos = 0;
    }

    public void Dispose()
    {
        Clear();
    }
}


