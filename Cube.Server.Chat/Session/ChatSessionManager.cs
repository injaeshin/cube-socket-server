using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using Cube.Core.Sessions;

namespace Cube.Server.Chat.Session;

public interface IChatSessionManager : ISessionManager
{
}

public class ChatSessionManager : SessionManager<ChatSession>, IChatSessionManager
{
    private readonly ILogger _logger;
    public ChatSessionManager(SessionHeartbeat heartbeatMonitor) : base(LoggerFactoryHelper.GetLoggerFactory(), heartbeatMonitor)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatSessionManager>();
    }

    protected override ChatSession CreateNewSession(Socket socket, SessionEventHandler events) => new(events);
}