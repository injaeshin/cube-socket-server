using Cube.Common.Interface;
using Microsoft.Extensions.Logging;

namespace Cube.Packet;

public interface IPacketDispatcher
{
    bool RegisterHandler(PacketType type, IPacketHandler handler);
    bool UnregisterHandler(PacketType type);
    bool TryGetHandler(PacketType type, out IPacketHandler handler);
    Task<bool> ProcessAsync(ISession session, PacketType packetType, ReadOnlyMemory<byte> payload);
}

public class PacketDispatcher : IPacketDispatcher
{
    private readonly ILogger<PacketDispatcher> _logger;
    private readonly Dictionary<PacketType, IPacketHandler> _handlers = [];

    public PacketDispatcher(ILogger<PacketDispatcher> logger)
    {
        _logger = logger;
    }

    public bool RegisterHandler(PacketType type, IPacketHandler handler)
    {
        if (_handlers.ContainsKey(type))
        {
            _logger.LogError("Handler for packet type {Type} already registered", type);
            return false;
        }

        _handlers[type] = handler;
        return true;
    }

    public bool UnregisterHandler(PacketType type)
    {
        return _handlers.Remove(type);
    }

    public bool TryGetHandler(PacketType type, out IPacketHandler handler)
    {
        handler = _handlers.GetValueOrDefault(type, new DefaultPacketHandler());
        return handler != null;
    }

    public async Task<bool> ProcessAsync(ISession session, PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        if (!TryGetHandler(packetType, out var handler))
        {
            _logger.LogError("No handler found for packet type {Type}", packetType);
            return false;
        }

        if (handler is IPayloadPacketHandler payloadHandler)
        {
            return await payloadHandler.HandleAsync(session, payload);
        }

        return await handler.HandleAsync(session);
    }
}
