using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using Common.Network.Session;
using Common.Network.Handler;
using Server.Chat.Helper;


namespace Server.Chat.Session;

public interface IChatSessionManager : INetSessionManager
{
    bool TryGetSession(string sessionId, out INetSession? session);
}

public class ChatSessionManager : NetSessionManager<ChatSession>, IChatSessionManager
{
    //private readonly ILogger<ChatSessionManager> _logger;
    private readonly IPacketDispatcher _packetDispatcher;

    public ChatSessionManager(ILoggerFactory loggerFactory, IPacketDispatcher packetDispatcher) : base(loggerFactory)
    {
        //_logger = LoggerFactoryHelper.CreateLogger<ChatSessionManager>();
        _packetDispatcher = packetDispatcher;
    }

    protected override ChatSession CreateNewSession(Socket socket, SessionEvents events)
    {
        return new ChatSession(_packetDispatcher, events);
    }

    public bool TryGetSession(string sessionId, out INetSession? session)
    {
        throw new NotImplementedException();
    }
}
