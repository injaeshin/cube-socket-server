using Microsoft.Extensions.Logging;
using Cube.Common.Interface;
using Cube.Packet;
using Cube.Server.Chat.Handler;

namespace Cube.Server.Chat.Service;

public class ChatService : IPacketDispatcher
{
    private readonly ILogger _logger;
    private readonly IServiceContext _managerContext;
    private readonly IPacketFactory _packetFactory;
    private readonly Dictionary<PacketType, IPacketHandler> _handlers;

    public ChatService(IObjectFactoryHelper objectFactory, IServiceContext managerContext, IPacketFactory packetFactory)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatService>();
        _managerContext = managerContext;
        _packetFactory = packetFactory;
        _handlers = new Dictionary<PacketType, IPacketHandler>
        {
            { PacketType.ChatMessage, objectFactory.Create<ChatMessageHandler>() },
        };
    }

    public IPacketHandler GetHandler(PacketType type)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        throw new NotImplementedException();
    }
}
