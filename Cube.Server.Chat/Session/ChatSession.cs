using Microsoft.Extensions.Logging;

using Cube.Core.Sessions;
using Cube.Server.Chat.Helper;

namespace Cube.Server.Chat.Session;

public interface IChatSession : ISession
{
}

public class ChatSession : NetSession, IChatSession
{
    private readonly ILogger _logger;

    public ChatSession(SessionEvent events) : base(LoggerFactoryHelper.GetLoggerFactory(), events)
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

    protected override bool OnPreProcessReceivedAsync(ReadOnlyMemory<byte> payload)
    {
        if (!base.OnPreProcessReceivedAsync(payload))
        {
            return false;
        }

        return true;
    }
}