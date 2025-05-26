using Microsoft.Extensions.Logging;

using Cube.Network;
using Cube.Network.PacketIO;
using Cube.Core.Handler;
using Cube.Core.Sessions;
using Cube.Common.Shared;
using Cube.Server.Chat.Service;
using Cube.Server.Chat.Helper;

namespace Server.Chat.Handler;

public class LoginHandler(IChatService chatService) : IPacketHandler
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<LoginHandler>();
    private readonly IChatService _chatService = chatService;

    public PacketType Type => PacketType.Login;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> packet)
    {
        if (session.IsAuthenticated)
        {
            _logger.LogError("Login packet received from authenticated session: {SessionId}", session.SessionId);
            session.Close(DisconnectReason.NotAuthorized);
            return false;
        }

        try
        {
            //_logger.LogDebug("Login packet raw data: {HexDump}, Length: {Length}",
            //    BitConverter.ToString(packet.ToArray()), packet.Length);

            var reader = new PacketReader(packet);
            if (reader.RemainingLength < 2)
            {
                _logger.LogError("Login packet does not contain string length field: {RemainingLength} bytes", reader.RemainingLength);
                return false;
            }

            var id = reader.ReadString();
            //_logger.LogInformation("Login: {Id}, Session: {SessionId}, Bytes: [{HexString}]", id, session.SessionId,
            //string.Join(" ", System.Text.Encoding.UTF8.GetBytes(id).Select(b => b.ToString("X2"))));

            if (!await _chatService.AddUser(id, session))
            {
                _logger.LogError("Failed to insert user: {SessionId}", session.SessionId);
                session.Close(DisconnectReason.Failed);
                return false;
            }

            session.Authenticate();

            var payload = new PacketWriter();
            payload.WriteType((ushort)PacketType.LoginSuccess);
            await session.SendAsync(payload);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing login packet: {ErrorMessage}", ex.Message);
            // 예외 정보와 함께 패킷 덤프
            _logger.LogDebug("Packet dump: {HexDump}", BitConverter.ToString(packet.ToArray()));
            return false;
        }
    }
}