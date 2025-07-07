using Microsoft.Extensions.Options;

namespace Cube.Core.Settings;

public interface ISettingsService
{
    NetworkConfig Network { get; }
    HeartbeatConfig Heartbeat { get; }
}

public class SettingsService : ISettingsService
{
    private readonly IOptions<CoreConfig> _config;

    public SettingsService(IOptions<CoreConfig> config)
    {
        _config = config;
    }

    public NetworkConfig Network => _config.Value.Network;
    public HeartbeatConfig Heartbeat => _config.Value.Heartbeat;
}