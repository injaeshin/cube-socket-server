using Microsoft.Extensions.Configuration;

namespace Cube.Core.Settings;

public sealed class AppSettings
{
    private static readonly Lazy<AppSettings> _instance = new(() => new AppSettings());
    public static AppSettings Instance => _instance.Value;

    private AppSettings() { }

    public NetworkConfig Network { get; private set; } = new();
    public HeartbeatConfig Heartbeat { get; private set; } = new();

    public static void Initialize(IConfiguration configuration)
    {
        var instance = Instance;

        configuration.GetSection(NetworkConfig.SectionName).Bind(instance.Network);
        configuration.GetSection(HeartbeatConfig.SectionName).Bind(instance.Heartbeat);

        Console.WriteLine($"Network: {instance.Network.MaxConnections}");
        Console.WriteLine($"Heartbeat: {instance.Heartbeat.Interval}");
    }
}