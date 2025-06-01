using Cube.Core;
using Cube.Core.Router;
using Cube.Core.Sessions;
using Cube.Packet;
using Microsoft.Extensions.Logging;

namespace Cube.Server.Chat.Session;

public interface IChatSession : ISession
{
}

public class ChatSession : Core.Sessions.Session, IChatSession
{
    private readonly ILogger _logger;

    public ChatSession(ILoggerFactory loggerFactory, IHeartbeat heartbeat, IFunctionRouter functionRouter)
        : base(loggerFactory.CreateLogger<Core.Sessions.Session>(), heartbeat, functionRouter)
    {
        _logger = loggerFactory.CreateLogger<ChatSession>();
    }

    protected override void OnConnected(ISession session, TransportType transportType)
    {
        base.OnConnected(session, transportType);
    }

    protected override void OnDisconnected(ISession session, TransportType transportType, bool isGraceful)
    {
        base.OnDisconnected(session, transportType, isGraceful);
    }

    protected override bool OnPreProcessReceivedAsync(PacketType packetType)
    {
        if (!base.OnPreProcessReceivedAsync(packetType))
        {
            return false;
        }

        return true;
    }
}