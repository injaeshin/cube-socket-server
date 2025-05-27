using Microsoft.Extensions.Logging;

using Cube.Common.Interface;
using Cube.Packet;

namespace Cube.Server.Chat.Handler;

public class LoginHandler : IPacketHandler
{
    private readonly ILogger _logger;

    public LoginHandler(ILogger<LoginHandler> logger)
    {
        _logger = logger;
    }

    public PacketType Type => PacketType.Login;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        _logger.LogInformation("Login: {SessionId}", session.SessionId);
        // if (session.IsAuthenticated)
        // {
        //     _logger.LogError("Login packet received from authenticated session: {SessionId}", session.SessionId);
        //     session.Close(new SessionClose(SocketDisconnect.NotAuthorized));
        //     return false;
        // }

        // try
        // {
        //     var reader = new PacketReader(payload);
        //     if (reader.RemainingLength < 2)
        //     {
        //         _logger.LogError("Login packet does not contain string length field: {RemainingLength} bytes", reader.RemainingLength);
        //         return false;
        //     }

        //     var id = reader.ReadString();
        //     if (!await _managerContext.UserManager.AddUserAsync(id, session))
        //     {
        //         _logger.LogError("Failed to insert user: {SessionId}", session.SessionId);
        //         session.Close(new SessionClose(SocketDisconnect.Failed));
        //         return false;
        //     }

        //     session.SetAuthenticated();
        //     return true;
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Error processing login packet for session: {SessionId}", session.SessionId);
        //     session.Close(new SessionClose(SocketDisconnect.Failed));
        //     return false;
        // }

        return await Task.FromResult(true);
    }
}