using System.Buffers;

namespace Cube.Core.Network;

public class PacketBuffer(int size = CoreConsts.BUFFER_SIZE)
{
    private readonly int _bufferSize = size;
    private readonly byte[] _buffer = new byte[size];

    private int _readPos = 0;
    private int _writePos = 0;

    public bool TryAppend(ReadOnlySpan<byte> data)
    {
        int freeSize = FreeSize();
        if (freeSize < data.Length)
        {
            //Console.WriteLine($"[PacketBuffer] TryAppend 실패: FreeSize={freeSize}, data.Length={data.Length}, _writePos={_writePos}, _readPos={_readPos}, DataSize={DataSize()}");
            return false;
        }

        int tailSpace = _bufferSize - _writePos;
        if (data.Length <= tailSpace)
        {
            // 한 번에 복사 가능
            data.CopyTo(_buffer.AsSpan(_writePos, data.Length));
        }
        else
        {
            // 두 번에 나눠서 복사
            data[..tailSpace].CopyTo(_buffer.AsSpan(_writePos, tailSpace));
            data[tailSpace..].CopyTo(_buffer.AsSpan(0, data.Length - tailSpace));
        }

        _writePos = (_writePos + data.Length) % _bufferSize;

        //Console.WriteLine($"[PacketBuffer] TryAppend 성공: FreeSize={freeSize}, data.Length={data.Length}, _writePos={_writePos}, _readPos={_readPos}, DataSize={DataSize()}");
        return true;
    }

    public bool TryAppend(Memory<byte> data, int writtenBytes)
    {
        int freeSize = FreeSize();
        if (freeSize < writtenBytes)
        {
            return false;
        }

        int tailSpace = _bufferSize - _writePos;
        if (writtenBytes <= tailSpace)
        {
            data.Span[..writtenBytes].CopyTo(_buffer.AsSpan(_writePos, writtenBytes));
        }
        else
        {
            data.Span[..tailSpace].CopyTo(_buffer.AsSpan(_writePos, tailSpace));
            data.Span[tailSpace..].CopyTo(_buffer.AsSpan(0, writtenBytes - tailSpace));
        }

        _writePos = (_writePos + writtenBytes) % _bufferSize;
        return true;
    }

    public bool TryAppend(ReadOnlyMemory<byte> data)
    {
        return TryAppend(data.Span);
    }

    public bool TryGetValidatePacket(out ushort packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    {
        packetType = 0;
        payload = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (DataSize() < CoreConsts.LENGTH_SIZE)
            return false;

        ushort bodyLength = ReadUInt16FromBuffer(0);
        if (bodyLength < CoreConsts.TYPE_SIZE || bodyLength > CoreConsts.BUFFER_SIZE - CoreConsts.TCP_HEADER_SIZE)
            return false;

        int totalPacketSize = CoreConsts.LENGTH_SIZE + bodyLength;  // 길이 + (타입 + 페이로드)
        if (DataSize() < totalPacketSize)
            return false;

        // 타입 읽기
        packetType = ReadUInt16FromBuffer(CoreConsts.LENGTH_SIZE);

        // 페이로드 크기 (타입 제외)
        int payloadLength = bodyLength - CoreConsts.TYPE_SIZE;
        rentedBuffer = ArrayPool<byte>.Shared.Rent(payloadLength);

        // 페이로드 시작 위치 (길이 + 타입 다음부터)
        int payloadStart = (_readPos + CoreConsts.LENGTH_SIZE + CoreConsts.TYPE_SIZE) % _bufferSize;
        if (_bufferSize - payloadStart >= payloadLength)
        {
            _buffer.AsSpan(payloadStart, payloadLength).CopyTo(rentedBuffer.AsSpan(0, payloadLength));
        }
        else
        {
            int firstPart = _bufferSize - payloadStart;
            _buffer.AsSpan(payloadStart, firstPart).CopyTo(rentedBuffer.AsSpan(0, firstPart));
            _buffer.AsSpan(0, payloadLength - firstPart).CopyTo(rentedBuffer.AsSpan(firstPart, payloadLength - firstPart));
        }

        payload = new ReadOnlyMemory<byte>(rentedBuffer, 0, payloadLength);  // 페이로드만
        Skip(totalPacketSize);
        return true;
    }


    public bool TryReadAsContinuous(int maxBytes, out ReadOnlyMemory<byte> chunk, out byte[]? rentedBuffer)
    {
        int dataSize = DataSize();
        rentedBuffer = null;
        if (dataSize == 0)
        {
            chunk = ReadOnlyMemory<byte>.Empty;
            return false;
        }
        int toRead = Math.Min(dataSize, maxBytes);

        if (_writePos > _readPos || _readPos + toRead <= _bufferSize)
        {
            // 연속된 메모리: 내부 버퍼 그대로 반환 (복사/임시 버퍼 불필요)
            chunk = new ReadOnlyMemory<byte>(_buffer, _readPos, toRead);
        }
        else
        {
            // 분리된 메모리: 임시 버퍼에 복사
            int firstPart = _bufferSize - _readPos;
            rentedBuffer = ArrayPool<byte>.Shared.Rent(toRead);
            _buffer.AsSpan(_readPos, firstPart).CopyTo(rentedBuffer.AsSpan(0, firstPart));
            _buffer.AsSpan(0, toRead - firstPart).CopyTo(rentedBuffer.AsSpan(firstPart, toRead - firstPart));
            chunk = new ReadOnlyMemory<byte>(rentedBuffer, 0, toRead);
        }

        Skip(toRead);
        return true;
    }

    private ushort ReadUInt16FromBuffer(int offset)
    {
        int realOffset = (_readPos + offset) % _bufferSize;
        int nextOffset = (_readPos + offset + 1) % _bufferSize;

        return (ushort)(_buffer[realOffset] << 8 | _buffer[nextOffset]);
    }

    public void Skip(int count)
    {
        _readPos = (_readPos + count) % _bufferSize;
    }

    public int FreeSize()
    {
        if (_writePos >= _readPos)
            return _bufferSize - (_writePos - _readPos) - 1;

        return _readPos - _writePos - 1;
    }

    public int DataSize()
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

    public static void Return(byte[]? rentedBuffer)
    {
        if (rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
