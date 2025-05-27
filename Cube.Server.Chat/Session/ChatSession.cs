using Microsoft.Extensions.Logging;

using Cube.Core.Sessions;
using Cube.Common.Interface;
using Cube.Server.Chat.Helper;
using Cube.Packet;

namespace Cube.Server.Chat.Session;

public interface IChatSession : ISession
{
}

public class ChatSession : Core.Sessions.Session, IChatSession
{
    private readonly ILogger _logger;

    public ChatSession(SessionEventHandler events) : base(LoggerFactoryHelper.GetLoggerFactory(), events)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatSession>();
    }

    protected override void OnConnected(ISession session)
    {
        base.OnConnected(session);
    }

    protected override void OnDisconnected(ISession session, bool isGraceful)
    {
        base.OnDisconnected(session, isGraceful);
    }

    protected override bool OnPreProcessReceivedAsync(ushort packetType, ReadOnlyMemory<byte> payload)
    {
        if (!base.OnPreProcessReceivedAsync(packetType, payload))
        {
            return false;
        }

        return true;
    }
}