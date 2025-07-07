namespace Cube.Core.Settings;

public class NetworkConfig
{
    public const string SectionName = "Network";

    public int ListenBacklog { get; set; }
    public int MaxConnections { get; set; }
    public int TcpAcceptPoolSize { get; set; }
    public int TcpSendPoolSize { get; set; }
    public int TcpReceivePoolSize { get; set; }

    public bool UdpEnabled { get; set; }
    public int UdpSendPoolSize { get; set; }
    public int UdpReceivePoolSize { get; set; }
    public int UdpResendIntervalMs { get; set; }
}

public class HeartbeatConfig
{
    public const string SectionName = "Heartbeat";

    public int Interval { get; set; }
    public int PingTimeout { get; set; }
    public int ResendIntervalMs { get; set; }
}

public class CoreConfig
{
    public NetworkConfig Network { get; set; } = new();
    public HeartbeatConfig Heartbeat { get; set; } = new();
}