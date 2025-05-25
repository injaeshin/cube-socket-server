//using Microsoft.Extensions.Logging;

//using Common.Network;
//using Common.Network.Handler;
//using Server.Chat.Helper;
//using Server.Chat.Service;
//using Common.Network.Session;


//namespace Server.Chat.Handler;

//public class LogoutHandler(IChatService chatService) : IPacketHandler
//{
//    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<LogoutHandler>();
//    private readonly IChatService _chatService = chatService;

//    public MessageType Type => MessageType.Logout;

//    public async Task<bool> HandleAsync(INetSession session, ReadOnlyMemory<byte> packet)
//    {
//        if (!session.IsAuthenticated)
//        {
//            _logger.LogError("Logout packet received from unauthenticated session: {SessionId}", session.SessionId);
//            session.Close(DisconnectReason.NotAuthenticated);
//            return false;
//        }

//        _logger.LogInformation("Logout: {SessionId}", session.SessionId);
//        await _chatService.DeleteUser(session.SessionId);
//        session.Deauthenticate();

//        return true;
//    }
//}