using Cube.Common.Interface;

namespace Cube.Packet;

public interface IPacketDispatcher
{
    IPacketHandler GetHandler(PacketType type);
    Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload);
}

// public class PacketDispatcher : IPacketDispatcher
// {
//     private readonly ILogger<PacketDispatcher> _logger;
//     private readonly Dictionary<PacketType, IPacketHandler> _handlers = [];

//     public PacketDispatcher(ILogger<PacketDispatcher> logger)
//     {
//         _logger = logger;
//     }

//     public bool RegisterHandler(PacketType type, IPacketHandler handler)
//     {
//         if (_handlers.ContainsKey(type))
//         {
//             _logger.LogError("Handler for packet type {Type} already registered", type);
//             return false;
//         }

//         _handlers[type] = handler;
//         return true;
//     }

//     public bool UnregisterHandler(PacketType type)
//     {
//         return _handlers.Remove(type);
//     }

//     public IPacketHandler GetHandler(PacketType type)
//     {
//         return _handlers.GetValueOrDefault(type, new DefaultPacketHandler());
//     }

//     public async Task<bool> ProcessAsync(ISession session, PacketType packetType, ReadOnlyMemory<byte> payload)
//     {
//         var handler = GetHandler(packetType);
//         return await handler.HandleAsync(session, payload);
//     }
//}
