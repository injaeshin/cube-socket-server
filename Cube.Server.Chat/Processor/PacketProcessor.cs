using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Common.Interface;
using Cube.Packet;
using Cube.Core.Network;
using Cube.Server.Chat.Helper;
using Cube.Server.Chat.Handler;


namespace Cube.Server.Chat.Processor;

public interface IPacketProcessor
{
    Task OnReceivedAsync(ReceivedContext context);
    Task ExecuteAsync(ReceivedContext context);
    Task OnSessionClosed(ISession session);
}

public class PacketProcessor : IPacketProcessor
{
    private readonly ILogger _logger;
    private readonly ProcessChannel _processChannel;
    private readonly ISessionManager _sessionManager;
    private readonly IPacketDispatcher _packetDispatcher;

    private LogoutHandler? _logoutHandler = null;

    public PacketProcessor(ILoggerFactory loggerFactory, IObjectFactoryHelper objectFactory, ISessionManager sessionManager)
    {
        _logger = loggerFactory.CreateLogger<PacketProcessor>();
        _processChannel = new ProcessChannel(loggerFactory, ExecuteAsync);
        _sessionManager = sessionManager;
        _packetDispatcher = objectFactory.Create<IPacketDispatcher>();

        RegisterHandler(objectFactory);
    }

    public async Task OnSessionClosed(ISession session)
    {
        if (_logoutHandler == null) throw new InvalidOperationException("Logout handler is not registered");

        await _logoutHandler.HandleAsync(session);
    }

    public async Task ExecuteAsync(ReceivedContext context)
    {
        if (!_sessionManager.TryGetSession(context.SessionId, out var session) || session == null)
        {
            _logger.LogError("Session not found: {SessionId}", context.SessionId);
            return;
        }

        if (!session.IsConnected)
        {
            _logger.LogError("Session is not alive: {SessionId}", context.SessionId);
            session.Close(new SessionClose(SocketDisconnect.NotConnected));
            return;
        }

        if (!PacketHelper.TryGetPacketType(context.Payload, out var type))
        {
            _logger.LogError("Invalid packet type / {SessionId}", context.SessionId);
            session.Close(new SessionClose(SocketDisconnect.InvalidPacket));
            return;
        }

        if (type > 0x0003 && !session.IsAuthenticated)
        {
            _logger.LogError("Session is not authenticated: {SessionId}", context.SessionId);
            session.Close(new SessionClose(SocketDisconnect.NotAuthenticated));
            return;
        }

        var packetType = (PacketType)type;
        if (!_packetDispatcher.TryGetHandler(packetType, out var handler))
        {
            _logger.LogError("Handler not found: {PacketType}", packetType);
            session.Close(new SessionClose(SocketDisconnect.InvalidType));
            return;
        }

        _logger.LogInformation("PacketProcessor ExecuteAsync: {PacketType}", packetType);

        if (handler is IPayloadPacketHandler payloadHandler)
        {
            await payloadHandler.HandleAsync(session, context.Payload);
            return;
        }

        await handler.HandleAsync(session);
    }

    public async Task OnReceivedAsync(ReceivedContext context)
    {
        await _processChannel.EnqueueAsync(context);
    }

    private void RegisterHandler(IObjectFactoryHelper objectFactory)
    {
        _packetDispatcher.RegisterHandler(PacketType.Login, objectFactory.Create<LoginHandler>());

        _logoutHandler = objectFactory.Create<LogoutHandler>();
        _packetDispatcher.RegisterHandler(PacketType.Logout, _logoutHandler);

        _packetDispatcher.RegisterHandler(PacketType.ChatMessage, objectFactory.Create<ChatMessageHandler>());
    }
}

