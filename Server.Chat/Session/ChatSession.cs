using Microsoft.Extensions.Logging;

using Cube.Session;
using Cube.Server.Chat.Helper;

namespace Cube.Server.Chat.Session;

public interface IChatSession : INetSession
{
}

public class ChatSession : NetSession, IChatSession
{
    private readonly ILogger _logger;

    public ChatSession(SessionEvent events) : base(LoggerFactoryHelper.GetLoggerFactory(), events)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatSession>();
    }

    protected override void OnConnected(INetSession session)
    {
        base.OnConnected(session);
    }

    protected override void OnDisconnected(INetSession session, bool isGraceful)
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