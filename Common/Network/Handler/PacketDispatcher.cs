using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Session;

namespace Common.Network.Handler;

public interface IPacketDispatcher
{
    bool TryGetHandler(PacketType type, out IPacketHandler handler);
    Task<bool> DispatchAsync(ISession session, PacketType type, ReadOnlyMemory<byte> payload);

    bool IsRegistered(PacketType type);
    void Register(PacketType type, IPacketHandler handler);
}

public class PacketDispatcher(ILogger<PacketDispatcher> logger) : IPacketDispatcher
{
    private readonly ILogger _logger = logger;
    private readonly Dictionary<PacketType, IPacketHandler> _handlers = [];

    public void Register(PacketType type, IPacketHandler handler)
    {
        _handlers.Add(type, handler);
    }

    public bool IsRegistered(PacketType type)
    {
        return _handlers.ContainsKey(type);
    }

    public bool TryGetHandler(PacketType type, out IPacketHandler handler)
    {
        handler = _handlers.GetValueOrDefault(type, new PacketDefaultHandler());
        return handler != null;
    }

    public async Task<bool> DispatchAsync(ISession session, PacketType type, ReadOnlyMemory<byte> payload)
    {
        if (!_handlers.TryGetValue(type, out var handler))
        {
            _logger.LogError("No handler found for packet type {Type}", type);
            return false;
        }

        return await handler.HandleAsync(session, payload);
    }
}