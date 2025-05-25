using Cube.Network;

namespace Cube.Network.Buffer;

public class RudpReceiveBuffer
{
    private ushort _expectedSequence = 0;
    private readonly SortedDictionary<ushort, ReadOnlyMemory<byte>> _outOfOrderPackets = new();
    private readonly PacketBuffer _buffer;

    public RudpReceiveBuffer()
    {
        _buffer = new PacketBuffer();
    }

    //public bool TryReadPacket(out MessageType packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    //{
    //    return _buffer.TryReadPacket(out packetType, out payload, out rentedBuffer);
    //}

    // 받은 패킷을 처리함
    public void UpdateReceived(ushort seq, ReadOnlyMemory<byte> data)
    {
        // 중복 또는 오래된 패킷이면 무시
        if (IsDuplicateOrOld(seq)) return;

        // 순서대로 받은 패킷이면 버퍼에 추가
        if (seq == _expectedSequence)
        {
            AppendAndAdvance(data);
            TryFlushBufferPackets();
        }
        else
        {
            // 순서대로 받은 패킷이 아니면 버퍼에 저장
            if (!_outOfOrderPackets.ContainsKey(seq))
            {
                _outOfOrderPackets[seq] = data;
            }
        }
    }

    private void AppendAndAdvance(ReadOnlyMemory<byte> data)
    {
        _buffer.TryAppend(data);
        _expectedSequence++;
    }

    private void TryFlushBufferPackets()
    {
        while (_outOfOrderPackets.TryGetValue(_expectedSequence, out var nextPayload))
        {
            _outOfOrderPackets.Remove(_expectedSequence);
            AppendAndAdvance(nextPayload);
        }
    }

    private bool IsDuplicateOrOld(ushort seq)
    {
        return (ushort)(seq - _expectedSequence) > 0x8000; // 32768
    }

    public ushort LastAck => (ushort)(_expectedSequence - 1);

    public void Reset()
    {
        _buffer.Reset();
        _expectedSequence = 0;
        _outOfOrderPackets.Clear();
    }

    public void Dispose()
    {
        Reset();
    }
}