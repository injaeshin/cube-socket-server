using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Session;

namespace Common.Network.Handler;

public interface IPacketDispatcher
{
    bool TryGetHandler(PacketType type, out IPacketHandler? handler);
    Task DispatchAsync(ISession session, ReadOnlyMemory<byte> packet);
    void Register(PacketType type, IPacketHandler handler);
}

public class PacketDispatcher : IPacketDispatcher
{
    private readonly ILogger _logger;
    private readonly Dictionary<PacketType, IPacketHandler> _handlers = [];

    public PacketDispatcher(ILogger<PacketDispatcher> logger)
    {
        _logger = logger;
    }

    public void Register(PacketType type, IPacketHandler handler)
    {
        _handlers.Add(type, handler);
    }

    public bool TryGetHandler(PacketType type, out IPacketHandler? handler)
    {
        return _handlers.TryGetValue(type, out handler);
    }

    public async Task DispatchAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        if (!PacketIO.TryParseHeader(packet, out _))
        {
            _logger.LogError("Invalid packet header");
            return;
        }

        var type = PacketIO.GetPacketType(packet);
        if (!_handlers.TryGetValue(type, out var handler))
        {
            _logger.LogError("No handler found for packet type {Type}", type);
            return;
        }

        await handler.HandleAsync(session, packet);
    }
}