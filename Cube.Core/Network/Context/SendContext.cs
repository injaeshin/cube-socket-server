using System.Net;
using System.Net.Sockets;
using Cube.Packet;

namespace Cube.Core.Network;

public class SendContext : IContext
{
    public string SessionId { get; init; }
    public Memory<byte> Data { get; init; }
    public byte[]? RentedBuffer { get; init; }

    public SendContext(string sessionId, Memory<byte> data, byte[]? rentedBuffer)
    {
        SessionId = sessionId;
        Data = data;
        RentedBuffer = rentedBuffer;
    }

    public void Return()
    {
        if (RentedBuffer != null)
        {
            BufferArrayPool.Return(RentedBuffer);
        }
    }
}

public class TcpSendContext : SendContext
{
    public Socket Socket { get; init; }

    public TcpSendContext(string sessionId, Memory<byte> data, byte[]? rentedBuffer, Socket socket)
        : base(sessionId, data, rentedBuffer)
    {
        Socket = socket;
    }
}

public class UdpSendContext : SendContext
{
    public EndPoint RemoteEndPoint { get; init; }
    public ushort Sequence { get; init; }

    public UdpSendContext(string sessionId, Memory<byte> data, byte[]? rentedBuffer, EndPoint remoteEndPoint, ushort sequence)
        : base(sessionId, data, rentedBuffer)
    {
        RemoteEndPoint = remoteEndPoint;
        Sequence = sequence;
    }

    public bool ShouldTrack() => Sequence > 0;
}
