using System.Buffers;
using System.Diagnostics;
using Common.Network.Packet;

namespace Common.Network.Buffer;

public class PacketBuffer
{
    private readonly byte[] _buffer = new byte[NetConsts.PACKET_BUFFER_SIZE];

    private int _readPos = 0;
    private int _writePos = 0;

    public bool TryAppend(ReadOnlySpan<byte> data)
    {
        if (FreeSize() < data.Length)
            return false;

        int firstCopyLength = Math.Min(data.Length, NetConsts.PACKET_BUFFER_SIZE - _writePos);
        data[..firstCopyLength].CopyTo(_buffer.AsSpan(_writePos));

        int remainLength = data.Length - firstCopyLength;
        if (remainLength > 0)
            data[firstCopyLength..].CopyTo(_buffer.AsSpan(0));

        _writePos = (_writePos + data.Length) % NetConsts.PACKET_BUFFER_SIZE;
        return true;
    }

    public bool TryAppend(ReadOnlyMemory<byte> data)
    {
        return TryAppend(data.Span);
    }

    public bool TryReadPacket(out MessageType packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    {
        packetType = MessageType.None;
        payload = ReadOnlyMemory<byte>.Empty;
        rentedBuffer = null;

        if (DataSize() < 2)
            return false;

        ushort length = ReadUInt16FromBuffer(0);
        if (length < 2 || length > NetConsts.MAX_PACKET_SIZE)
            return false;

        int totalPacketSize = 2 + length;
        if (DataSize() < totalPacketSize)
            return false;

        ushort type = ReadUInt16FromBuffer(2);
        packetType = (MessageType)type;
        int payloadLength = length - 2;
        if (payloadLength == 0)
        {
            Skip(totalPacketSize);
            return true;
        }

        int payloadStart = (_readPos + 4) % NetConsts.PACKET_BUFFER_SIZE;
        if (NetConsts.PACKET_BUFFER_SIZE - payloadStart >= payloadLength)
        {
            payload = new ReadOnlyMemory<byte>(_buffer, payloadStart, payloadLength);
            rentedBuffer = null;
        }
        else
        {
            int firstPart = NetConsts.PACKET_BUFFER_SIZE - payloadStart;
            rentedBuffer = ArrayPool<byte>.Shared.Rent(payloadLength);
            _buffer.AsSpan(payloadStart, firstPart).CopyTo(rentedBuffer.AsSpan(0, firstPart));
            _buffer.AsSpan(0, payloadLength - firstPart).CopyTo(rentedBuffer.AsSpan(firstPart, payloadLength - firstPart));
            payload = new ReadOnlyMemory<byte>(rentedBuffer, 0, payloadLength);
        }

        Skip(totalPacketSize);
        return true;
    }

    private ushort ReadUInt16FromBuffer(int offset)
    {
        int realOffset = (_readPos + offset) % NetConsts.PACKET_BUFFER_SIZE;
        int nextOffset = (_readPos + offset + 1) % NetConsts.PACKET_BUFFER_SIZE;

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

    public void Skip(int count)
    {
        _readPos = (_readPos + count) % NetConsts.PACKET_BUFFER_SIZE;
    }

    private int FreeSize()
    {
        if (_writePos >= _readPos)
            return NetConsts.PACKET_BUFFER_SIZE - (_writePos - _readPos) - 1;

        return _readPos - _writePos - 1;
    }

    private int DataSize()
    {
        if (_writePos >= _readPos)
            return _writePos - _readPos;

        return NetConsts.PACKET_BUFFER_SIZE - _readPos + _writePos;
    }

    public void Reset()
    {
        _readPos = 0;
        _writePos = 0;
        Debug.WriteLine("[PacketBuffer] Reset: Buffer has been reset");
    }

    public bool TryRead(int maxBytes, out ReadOnlyMemory<byte> chunk, out byte[]? rentedBuffer)
    {
        int dataSize = DataSize();
        rentedBuffer = null;
        if (dataSize == 0)
        {
            chunk = ReadOnlyMemory<byte>.Empty;
            return false;
        }
        int toRead = Math.Min(dataSize, maxBytes);
        if (_writePos > _readPos || _readPos + toRead <= NetConsts.PACKET_BUFFER_SIZE)
        {
            chunk = new ReadOnlyMemory<byte>(_buffer, _readPos, toRead);
        }
        else
        {
            int firstPart = NetConsts.PACKET_BUFFER_SIZE - _readPos;
            rentedBuffer = ArrayPool<byte>.Shared.Rent(toRead);
            _buffer.AsSpan(_readPos, firstPart).CopyTo(rentedBuffer.AsSpan(0, firstPart));
            _buffer.AsSpan(0, toRead - firstPart).CopyTo(rentedBuffer.AsSpan(firstPart, toRead - firstPart));
            chunk = new ReadOnlyMemory<byte>(rentedBuffer, 0, toRead);
        }
        return true;
    }
}
