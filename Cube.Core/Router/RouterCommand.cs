using System.Net;
using System.Net.Sockets;
using Cube.Core.Network;

namespace Cube.Core.Router;

// Tcp 연결 이벤트 (NetworkManager)
public readonly struct ClientAcceptedCmd
{
    public Socket Socket { get; init; }

    public ClientAcceptedCmd(Socket socket)
    {
        Socket = socket;
    }
}

// Udp 데이터 수신 이벤트 (NetworkManager)
public readonly struct ClientDatagramReceivedCmd
{
    public UdpReceivedContext Context { get; init; }

    public ClientDatagramReceivedCmd(UdpReceivedContext context)
    {
        Context = context;
    }
}

// Tcp 연결 이벤트 (SessionManager)
public readonly struct TcpConnectedCmd
{
    public ITcpConnection Connection { get; init; }

    public TcpConnectedCmd(ITcpConnection connection)
    {
        Connection = connection;
    }
}

// Udp 연결 이벤트 (SessionManager)
public readonly struct UdpConnectedCmd
{
    public string SessionId { get; init; }
    public ushort Sequence { get; init; }
    public EndPoint Endpoint { get; init; }
    public IUdpConnection Connection { get; init; }

    public UdpConnectedCmd(string sessionId, ushort sequence, EndPoint endpoint, IUdpConnection connection)
    {
        SessionId = sessionId;
        Sequence = sequence;
        Endpoint = endpoint;
        Connection = connection;
    }
}

// Udp 데이터 수신 이벤트 (SessionManager)
public readonly struct UdpReceivedCmd
{
    public UdpReceivedContext Context { get; init; }

    public UdpReceivedCmd(UdpReceivedContext context)
    {
        Context = context;
    }
}

// Session 반환 이벤트 (SessionManager)
public readonly struct SessionReturnCmd
{
    public string SessionId { get; init; }

    public SessionReturnCmd(string sessionId)
    {
        SessionId = sessionId;
    }
}

// Tcp 데이터 전송 이벤트 (NetworkService)
public readonly struct TcpSendCmd
{
    public TcpSendContext Context { get; init; }

    public TcpSendCmd(TcpSendContext context)
    {
        Context = context;
    }
}

// Udp 데이터 전송 이벤트 (NetworkService)
public readonly struct UdpSendCmd
{
    public UdpSendContext Context { get; init; }

    public UdpSendCmd(UdpSendContext context)
    {
        Context = context;
    }
}

// Udp Ack 수신 이벤트 (SessionManager)
public readonly struct UdpReceivedAckCmd
{
    public string SessionId { get; init; }
    public ushort Ack { get; init; }

    public UdpReceivedAckCmd(string sessionId, ushort ack)
    {
        SessionId = sessionId;
        Ack = ack;
    }
}

// Udp Track 전송 이벤트 (SessionManager)
public readonly struct UdpTrackSentCmd
{
    public UdpSendContext Context { get; init; }

    public UdpTrackSentCmd(UdpSendContext context)
    {
        Context = context;
    }
}

// 데이터 수신 이벤트 (NetworkService)
public readonly struct ReceivedEnqueueCmd
{
    public ReceivedContext Context { get; init; }

    public ReceivedEnqueueCmd(ReceivedContext context)
    {
        Context = context;
    }
}

