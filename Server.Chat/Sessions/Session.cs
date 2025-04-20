using Microsoft.Extensions.Logging;

using Server.Core.Session;

namespace Server.Chat.Sessions;

public class Session : SocketSession
{
    private readonly ILogger _logger;
    public Session(SocketSessionOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory.CreateLogger<SocketSession>())
    {
        _logger = loggerFactory.CreateLogger<Session>();
    }

    protected override void OnConnect(ISocketSession session)
    {
        _logger.LogInformation("세션 연결됨: {SessionId}", SessionId);
    }

    protected override void OnDisconnect(ISocketSession session, DisconnectReason reason)
    {
        _logger.LogInformation("세션 연결 끊김: {SessionId} - 이유: {Reason}", SessionId, reason);
    }
}