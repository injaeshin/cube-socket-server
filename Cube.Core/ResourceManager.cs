using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Cube.Core.Settings;

namespace Cube.Core;

public interface IAsyncResource
{
    string Name { get; }
    int Count { get; }
    int InUse { get; }

    Task StopAsync(TimeSpan timeout);
}

public interface IResourceManager
{
    Task ShutdownAsync(TimeSpan timeoutPerResource);
    IPoolHandler<SocketAsyncEventArgs> GetPoolHandler(ResourceKey resourceKey);
    IAsyncResource GetResource(ResourceKey resourceKey);

    IReadOnlyDictionary<string, string> Status { get; }
}

public class ResourceManager : IResourceManager
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<ResourceKey, IAsyncResource> _resources = [];
    private readonly ConcurrentDictionary<string, string> _status = new();

    public ResourceManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ResourceManager>();
        _loggerFactory = loggerFactory;

        CreateResources(loggerFactory);
    }

    private void CreateResources(ILoggerFactory loggerFactory)
    {
        var saeaPool = new SocketAsyncEventArgsPool(loggerFactory, AppSettings.Instance.Network.MaxConnections);
        var saeaPoolHandler = new SAEAPoolHandler(saeaPool);

        CreateResource(ResourceKey.SocketAsyncEventArgsPool, saeaPool);
        CreateResource(ResourceKey.TcpConnectionPool, new TcpConnectionPool(loggerFactory, saeaPoolHandler, AppSettings.Instance.Network.MaxConnections));

        if (AppSettings.Instance.Network.UdpEnabled)
        {
            CreateResource(ResourceKey.UdpConnectionPool, new UdpConnectionPool(loggerFactory));
        }
    }

    private void CreateResource(ResourceKey resourceKey, IAsyncResource resource)
    {
        if (_resources.ContainsKey(resourceKey))
        {
            _logger.LogWarning("Resource {ResourceKey} is already registered", resourceKey);
            throw new InvalidOperationException($"Resource {resourceKey} is already registered");
        }

        _resources[resourceKey] = resource;
    }

    public IPoolHandler<SocketAsyncEventArgs> GetPoolHandler(ResourceKey resourceKey)
    {
        if (!_resources.TryGetValue(resourceKey, out var resource))
        {
            throw new InvalidOperationException($"Resource {resourceKey} is not registered");
        }

        if (resource is not SocketAsyncEventArgsPool saeaPool)
        {
            throw new InvalidOperationException($"Resource {resourceKey} is not a SocketAsyncEventArgsPool");
        }

        return new SAEAPoolHandler(saeaPool);
    }

    public IAsyncResource GetResource(ResourceKey resourceKey)
    {
        if (!_resources.TryGetValue(resourceKey, out var resource))
        {
            throw new InvalidOperationException($"Resource {resourceKey} is not registered");
        }

        return resource;
    }

    public async Task ShutdownAsync(TimeSpan timeoutPerResource)
    {
        foreach (var key in Enum.GetValues<ResourceKey>().Reverse())
        {
            if (!_resources.TryGetValue(key, out var resource)) continue;

            var name = key.ToString();

            try
            {
                using var cts = new CancellationTokenSource(timeoutPerResource);
                _logger.LogInformation("Shutting down resource: {Name}", name);
                await resource.StopAsync(timeoutPerResource);
                _status[name] = "Shutdown Complete";
                _logger.LogInformation("Shutdown complete: {Name}", name);
            }
            catch (OperationCanceledException)
            {
                _status[name] = "Timeout";
                _logger.LogWarning("Shutdown timeout: {Name}", name);
            }
            catch (Exception ex)
            {
                _status[name] = $"Failed: {ex.Message}";
                _logger.LogError(ex, "Error shutting down resource: {Name}", name);
            }
        }
    }

    public IReadOnlyDictionary<string, string> Status => _status;
}