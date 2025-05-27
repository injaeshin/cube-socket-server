using Microsoft.Extensions.Logging;
using Cube.Common.Interface;
using Cube.Packet;
using Cube.Server.Chat.Handler;

namespace Cube.Server.Chat.Service;

public class AuthService : IPacketDispatcher
{
    private readonly ILogger _logger;
    private readonly IServiceContext _managerContext;
    private readonly IPacketFactory _packetFactory;
    private readonly Dictionary<PacketType, IPacketHandler> _handlers;

    public AuthService(IObjectFactoryHelper objectFactory, IServiceContext managerContext, IPacketFactory packetFactory)
    {
        _logger = LoggerFactoryHelper.CreateLogger<AuthService>();
        _managerContext = managerContext;
        _packetFactory = packetFactory;
        _handlers = new Dictionary<PacketType, IPacketHandler>
        {
            { PacketType.Login, objectFactory.Create<LoginHandler>() },
            { PacketType.Logout, objectFactory.Create<LogoutHandler>() },
        };
    }

    public IPacketHandler GetHandler(PacketType type)
    {
        return _handlers.GetValueOrDefault(type, new DefaultPacketHandler());
    }

    public async Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        if (!_managerContext.SessionManager.TryGetSession(sessionId, out var session))
        {
            return false;
        }

        var handler = GetHandler(packetType);
        return await handler.HandleAsync(session!, payload);
    }
}
