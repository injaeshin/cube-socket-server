using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace Cube.Network.Context;

public class TcpSendContext
{
    public string SessionId { get; init; }
    public Socket Socket { get; init; }
    public Memory<byte> Data { get; init; }
    public byte[]? RentedBuffer { get; init; }

    public Func<TcpSendContext, Task>? OnSendCompleted { get; set; }

    public TcpSendContext(string sessionId, Memory<byte> data, byte[]? rentedBuffer, Socket socket)
    {
        SessionId = sessionId;
        Socket = socket;
        Data = data;
        RentedBuffer = rentedBuffer;
    }

    public void Return()
    {
        if (RentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(RentedBuffer);
        }
    }
}

public class UdpSendContext
{
    public string SessionId { get; init; }
    public Memory<byte> Data { get; init; }
    public byte[]? RentedBuffer { get; init; }
    public EndPoint? RemoteEndPoint { get; init; }
    public ushort Sequence { get; init; }
    public ushort Ack { get; init; }

    public Socket? Socket { get; set; }
    public Func<string, ushort, ReadOnlyMemory<byte>, bool>? OnUdpPreDatagramSend { get; set; }

    public UdpSendContext(string sessionId, Memory<byte> data, byte[]? rentedBuffer, EndPoint remoteEndPoint, ushort sequence, ushort ack)
    {
        SessionId = sessionId;
        Data = data;
        RentedBuffer = rentedBuffer;
        RemoteEndPoint = remoteEndPoint;
        Sequence = sequence;
        Ack = ack;
    }

    public void Return()
    {
        if (RentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(RentedBuffer);
        }
    }
}
