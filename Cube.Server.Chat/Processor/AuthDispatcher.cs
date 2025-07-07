using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Packet;
using Cube.Server.Chat.Model;
using Cube.Packet.Builder;


namespace Cube.Server.Chat.Processor;

internal class AuthDispatcher : Dispatcher
{
    private readonly ILogger _logger;

    public AuthDispatcher(IManagerContext managerContext) : base(managerContext)
    {
        _logger = LoggerFactoryHelper.CreateLogger<AuthDispatcher>();

        _handlers = new()
        {
            { PacketType.Login, HandleLoginAsync },
            { PacketType.Logout, HandleLogoutAsync },
        };
    }

    public override async Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        if (!_managerContext.SessionManager.TryGetSession(sessionId, out var session))
        {
            return false;
        }

        if (!_handlers.TryGetValue(packetType, out var handler))
        {
            _logger.LogError("Handler not found for packet type: {PacketType}", packetType);
            return false;
        }

        return packetType switch
        {
            PacketType.Login => await handler(session, payload),
            PacketType.Logout => await handler(session, Memory<byte>.Empty),
            _ => false,
        };
    }

    private async Task<bool> HandleLoginAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        _logger.LogInformation("Login: {SessionId}", session.SessionId);

        if (session.IsAuthenticated)
        {
            _logger.LogError("Login packet received from authenticated session: {SessionId}", session.SessionId);
            return false;
        }

        try
        {
            var login = Login.Read(payload);
            if (login == null)
            {
                _logger.LogError("Login packet does not contain string length field: {RemainingLength} bytes", payload.Length);
                return false;
            }

            if (!_managerContext.UserManager.TryInsertUser(login.Username, session, out _))
            {
                _logger.LogError("Failed to insert user: {SessionId}", session.SessionId);
                return false;
            }

            session.SetAuthenticated();
            var (data, rentedBuffer) = new PacketWriter(PacketType.LoginSuccess)
                                            .WriteString(session.SessionId)
                                            .ToTcpPacket();
            await session.SendAsync(data, rentedBuffer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing login packet for session: {SessionId}", session.SessionId);
            return false;
        }

        return true;
    }

    private async Task<bool> HandleLogoutAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        _logger.LogInformation("Logout: {SessionId}", session.SessionId);

        _managerContext.UserManager.DeleteUser(session.SessionId);

        return await Task.FromResult(true);
    }
}
