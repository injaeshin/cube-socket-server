using Microsoft.Extensions.Logging;

using Cube.Network.Channel;
using Cube.Network.Context;
using Cube.Core.Sessions;
using Cube.Core.Dispatcher;
using Cube.Server.Chat.Helper;
using Cube.Common.Shared;
using Server.Chat.Handler;
using Cube.Server.Chat.Handler;
using Cube.Network.PacketIO;
using Cube.Network;

namespace Cube.Server.Chat.Processor;

public interface IPacketProcessor
{
    Task EnqueueAsync(string sessionId, ReadOnlyMemory<byte> data, byte[]? rentedBuffer);
    Task ExecuteAsync(ReceivedContext context);
}

public class PacketProcessor : IPacketProcessor
{
    private readonly ILogger _logger;
    private readonly ProcessChannel _processChannel;
    private readonly ISessionManager _sessionManager;
    private readonly IPacketDispatcher _packetDispatcher;
    public PacketProcessor(ILoggerFactory loggerFactory, IObjectFactoryHelper objectFactory, ISessionManager sessionManager)
    {
        _logger = loggerFactory.CreateLogger<PacketProcessor>();
        _processChannel = new ProcessChannel(loggerFactory, ExecuteAsync);
        _sessionManager = sessionManager;
        _packetDispatcher = objectFactory.Create<IPacketDispatcher>();

        RegisterHandler(objectFactory);
    }

    public async Task ExecuteAsync(ReceivedContext context)
    {
        if (!_sessionManager.TryGetSession(context.SessionId, out var session) || session == null)
        {
            _logger.LogError("Session not found: {SessionId}", context.SessionId);
            return;
        }

        if (!session.IsConnectionAlive())
        {
            _logger.LogError("Session is not alive: {SessionId}", context.SessionId);
            session.Close(DisconnectReason.NotConnected);
            return;
        }

        if (!PacketHelper.TryGetPacketType(context.Data, out var type))
        {
            _logger.LogError("Invalid packet type / {SessionId}", context.SessionId);
            session.Close(DisconnectReason.InvalidPacket);
            return;
        }

        if (type > 0x0003 && !session.IsAuthenticated)
        {
            _logger.LogError("Session is not authenticated: {SessionId}", context.SessionId);
            session.Close(DisconnectReason.NotAuthenticated);
            return;
        }

        var packetType = (PacketType)type;
        if (!_packetDispatcher.TryGetHandler(packetType, out var handler))
        {
            _logger.LogError("Handler not found: {PacketType}", packetType);
            session.Close(DisconnectReason.InvalidType);
            return;
        }

        _logger.LogInformation("PacketProcessor ExecuteAsync: {PacketType}", packetType);

        await handler.HandleAsync(session, context.Data);
    }

    public async Task EnqueueAsync(string sessionId, ReadOnlyMemory<byte> data, byte[]? rentedBuffer)
    {
        var context = new ReceivedContext(sessionId, data, rentedBuffer);
        await _processChannel.EnqueueAsync(context);
    }

    private void RegisterHandler(IObjectFactoryHelper objectFactory)
    {
        _packetDispatcher.Register(PacketType.Login, objectFactory.Create<LoginHandler>());
        _packetDispatcher.Register(PacketType.Logout, objectFactory.Create<LogoutHandler>());
        _packetDispatcher.Register(PacketType.ChatMessage, objectFactory.Create<ChatMessageHandler>());
    }

}

