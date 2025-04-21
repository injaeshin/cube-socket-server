using Microsoft.Extensions.Logging;

using Common.Network.Packet;
using Common.Network.Session;
using Common.Network.Transport;

namespace Common.Handler;

public class PacketHandlerDispatcher
{
    private readonly ILogger<PacketHandlerDispatcher> _logger;
    private readonly Dictionary<PacketType, Func<SendPacket, Task>> _handlers = new();

    public PacketHandlerDispatcher(ILogger<PacketHandlerDispatcher> logger)
    {
        _logger = logger;
    }

    public void Register(PacketType type, Func<SendPacket, Task> handler)
    {
        _handlers.Add(type, handler);
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

        var context = new SendPacket(session, type, PacketIO.GetPayload(packet));
        await handler(context);
    }
}