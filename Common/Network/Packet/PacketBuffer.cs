using System;
using System.Buffers;
using System.Diagnostics;

namespace Common.Network.Packet;

public class PacketBuffer
{
    private readonly byte[] _buffer = new byte[Constant.PACKET_BUFFER_SIZE];

    private int _readPos = 0;
    private int _writePos = 0;

    public bool TryAppend(ReadOnlySpan<byte> data)
    {
        if (FreeSize() < data.Length)
            return false;

        int firstCopyLength = Math.Min(data.Length, Constant.PACKET_BUFFER_SIZE - _writePos);
        data[..firstCopyLength].CopyTo(_buffer.AsSpan(_writePos));

        int remainLength = data.Length - firstCopyLength;
        if (remainLength > 0)
            data[firstCopyLength..].CopyTo(_buffer.AsSpan(0));

        _writePos = (_writePos + data.Length) % Constant.PACKET_BUFFER_SIZE;
        return true;
    }

    public bool TryAppend(ReadOnlyMemory<byte> data)
    {
        return TryAppend(data.Span);
    }

    public bool TryReadPacket(out ReadOnlyMemory<byte> packet, out byte[]? rentedBuffer)
    {
        packet = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (!TryPeekUShort(out ushort bodySize))
        {
            Debug.WriteLine($"[PacketBuffer] TryReadPacket: Length field not available, DataSize={DataSize()}");
            return false;
        }

        if (bodySize == 0 || bodySize + Constant.LENGTH_SIZE > Constant.PACKET_BUFFER_SIZE)
        {
            Debug.WriteLine($"[PacketBuffer] TryReadPacket: Invalid body size: {bodySize}");
            return false;
        }

        if (DataSize() < Constant.LENGTH_SIZE + bodySize)
        {
            Debug.WriteLine($"[PacketBuffer] TryReadPacket: Not enough data. Need={Constant.LENGTH_SIZE + bodySize}, Available={DataSize()}");
            return false;
        }

        // 패킷 크기 로깅
        Debug.WriteLine($"[PacketBuffer] TryReadPacket: Reading packet - BodySize={bodySize}, ReadPos={_readPos}, WritePos={_writePos}");

        // Skip length field without modifying _readPos yet
        int packetStartPos = (_readPos + Constant.LENGTH_SIZE) % Constant.PACKET_BUFFER_SIZE;

        rentedBuffer = ArrayPool<byte>.Shared.Rent(bodySize);

        if (Constant.PACKET_BUFFER_SIZE - packetStartPos >= bodySize)
        {
            // 연속된 메모리 공간에서 읽는 경우
            _buffer.AsSpan(packetStartPos, bodySize).CopyTo(rentedBuffer);
            
            // 디버깅용 - 읽은 처음 몇 바이트 로깅
            if (bodySize > 0)
            {
                var logBytes = Math.Min((int)bodySize, (int)16);
                var logData = BitConverter.ToString(_buffer.AsSpan(packetStartPos, logBytes).ToArray());
                Debug.WriteLine($"[PacketBuffer] Read continuous: {logData}{(bodySize > 16 ? "..." : "")}");
            }
        }
        else
        {
            // 버퍼의 끝과 시작에 걸쳐 있는 경우
            int firstPart = Constant.PACKET_BUFFER_SIZE - packetStartPos;
            _buffer.AsSpan(packetStartPos, firstPart).CopyTo(rentedBuffer);
            _buffer.AsSpan(0, bodySize - firstPart).CopyTo(rentedBuffer.AsSpan(firstPart));
            
            // 디버깅용 - 읽은 처음 몇 바이트 로깅 (두 부분으로 나누어 읽은 경우)
            var firstBytes = BitConverter.ToString(_buffer.AsSpan(packetStartPos, Math.Min(firstPart, 8)).ToArray());
            var secondBytes = bodySize - firstPart > 0 
                ? BitConverter.ToString(_buffer.AsSpan(0, Math.Min(bodySize - firstPart, 8)).ToArray()) 
                : "";
            Debug.WriteLine($"[PacketBuffer] Read split: first({firstPart})={firstBytes}, second({bodySize - firstPart})={secondBytes}");
        }

        packet = new ReadOnlyMemory<byte>(rentedBuffer, 0, bodySize);
        Skip(Constant.LENGTH_SIZE + bodySize);
        return true;
    }

    private ushort ReadUInt16FromBuffer(int offset)
    {
        int realOffset = (_readPos + offset) % Constant.PACKET_BUFFER_SIZE;
        int nextOffset = (_readPos + offset + 1) % Constant.PACKET_BUFFER_SIZE;

        return (ushort)(_buffer[realOffset] << 8 | _buffer[nextOffset]);
    }

    private bool TryPeekUShort(out ushort value)
    {
        value = 0;

        if (DataSize() < 2)
            return false;

        value = ReadUInt16FromBuffer(0);
        return true;
    }

    private void Skip(int count)
    {
        _readPos = (_readPos + count) % Constant.PACKET_BUFFER_SIZE;
    }

    private int FreeSize()
    {
        if (_writePos >= _readPos)
            return Constant.PACKET_BUFFER_SIZE - (_writePos - _readPos) - 1;

        return _readPos - _writePos - 1;
    }

    private int DataSize()
    {
        if (_writePos >= _readPos)
            return _writePos - _readPos;

        return Constant.PACKET_BUFFER_SIZE - _readPos + _writePos;
    }

    public void Reset()
    {
        _readPos = 0;
        _writePos = 0;
        Debug.WriteLine("[PacketBuffer] Reset: Buffer has been reset");
    }
}
