namespace Cube.Core;

public enum TransportType
{
    None = 0,
    Tcp = 1,
    Udp = 2,
}

public enum SessionStateType
{
    None,
    Connected,
    Disconnected,
    Authenticated,
}

public enum HeartbeatStateType
{
    None,
    Active,
    PingSent,
    PingReceived,
    Timeout,
}

public enum ResourceKey
{
    SocketAsyncEventArgsPool,
    TcpConnectionPool,
    UdpConnectionPool,
}