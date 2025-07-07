using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Core.Router;
using Cube.Core.Settings;

namespace Cube.Server.Chat.Session;

public interface IChatSessionManager : ISessionManager
{
}

public class ChatSessionManager : SessionManager<ChatSession>, IChatSessionManager
{
    private readonly ILogger _logger;

    public ChatSessionManager(ILoggerFactory loggerFactory, IFunctionRouter functionRouter, IHeartbeat heartbeatMonitor)
        : base(loggerFactory, functionRouter, heartbeatMonitor)
    {
        _logger = loggerFactory.CreateLogger<ChatSessionManager>();
    }

    protected override ChatSession CreateSession(ILoggerFactory loggerFactory, IHeartbeat heartbeat, IFunctionRouter functionRouter)
        => new(loggerFactory, heartbeat, functionRouter);
}