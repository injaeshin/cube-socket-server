using Microsoft.Extensions.Logging;

using Common.Network;
using Common.Network.Session;

namespace Server.Chat.Sessions;

public class Session : SocketSession
{
    private readonly ILogger _logger;
    public Session(SocketSessionOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory.CreateLogger<SocketSession>())
    {
        _logger = loggerFactory.CreateLogger<Session>();
    }

    protected override void OnConnect(ISession session)
    {
        _logger.LogInformation("세션 연결됨: {SessionId}", SessionId);
    }

    protected override void OnDisconnect(ISession session, DisconnectReason reason)
    {
        _logger.LogInformation("세션 연결 끊김: {SessionId} - 이유: {Reason}", SessionId, reason);
    }
}