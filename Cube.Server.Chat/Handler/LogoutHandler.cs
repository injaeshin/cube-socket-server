using Microsoft.Extensions.Logging;
using Cube.Common.Interface;
using Cube.Packet;

namespace Cube.Server.Chat.Handler;

public class LogoutHandler : IPacketHandler
{
    private readonly ILogger _logger;

    public LogoutHandler(ILogger<LogoutHandler> logger)
    {
        _logger = logger;
    }

    public PacketType Type => PacketType.Logout;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        _logger.LogInformation("Logout: {SessionId}", session.SessionId);
        session.SetDisconnected();

        return await Task.FromResult(true);
    }
}