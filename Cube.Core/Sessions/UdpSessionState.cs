using System.Net;
using Cube.Network.Buffer;

namespace Cube.Core.Sessions;

public class UdpSessionState
{
    public string SessionId { get; set; }
    public EndPoint RemoteEndPoint { get; set; }

    private readonly RudpReceiveBuffer _udpReceiveBuffer;
    private readonly RudpSendBuffer _udpSendBuffer;

    public UdpSessionState(string sessionId, EndPoint remoteEndPoint, Func<EndPoint, ushort, ReadOnlyMemory<byte>, Task> resendFunc)
    {
        SessionId = sessionId;
        RemoteEndPoint = remoteEndPoint;

        _udpReceiveBuffer = new RudpReceiveBuffer();
        _udpSendBuffer = new RudpSendBuffer(resendFunc, () => RemoteEndPoint);
    }

    public void UpdateReceived(ushort seq, ReadOnlyMemory<byte> data)
    {
        _udpReceiveBuffer.UpdateReceived(seq, data);
    }

    public void Acknowledge(ushort seq)
    {
        _udpSendBuffer.Acknowledge(seq);
    }

    //public bool TryReadPacket(out MessageType packetType, out ReadOnlyMemory<byte> payload, out byte[]? rentedBuffer)
    //{
    //    return _udpReceiveBuffer.TryReadPacket(out packetType, out payload, out rentedBuffer);
    //}

    public void Track(ushort seq, ReadOnlyMemory<byte> payload)
    {
        _udpSendBuffer.Track(seq, payload);
    }

    public ushort NextSequence => _udpSendBuffer.NextSequence;

    public ushort LastAck => _udpReceiveBuffer.LastAck;

    public void Reset()
    {
        SessionId = string.Empty;
        RemoteEndPoint = null!;
        _udpReceiveBuffer.Reset();
        _udpSendBuffer.Reset();
    }

    public void Dispose()
    {
        _udpReceiveBuffer.Dispose();
        _udpSendBuffer.Dispose();
    }

}