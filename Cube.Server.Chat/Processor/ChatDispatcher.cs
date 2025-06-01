using Microsoft.Extensions.Logging;
using Cube.Packet;
using Cube.Server.Chat.Model;
using Cube.Server.Chat.Service;
using Cube.Core;

namespace Cube.Server.Chat.Processor;

internal class ChatDispatcher : Dispatcher
{
    private readonly ILogger _logger;
    private readonly IChatService _chatService;

    public ChatDispatcher(IManagerContext managerContext, IChatService chatService) : base(managerContext)
    {
        _logger = LoggerFactoryHelper.CreateLogger<ChatDispatcher>();
        _chatService = chatService;

        _handlers = new()
        {
            { PacketType.ChatMessage, HandleChatMessageAsync },
        };
    }

    public override async Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload)
    {
        if (!_managerContext.SessionManager.TryGetSession(sessionId, out var session))
        {
            _logger.LogError("Session not found: {SessionId}", sessionId);
            return false;
        }

        if (!session.IsAuthenticated)
        {
            _logger.LogError("Chat message received from unauthenticated session: {SessionId}", session.SessionId);
            return false;
        }

        if (!_handlers.TryGetValue(packetType, out var handler))
        {
            _logger.LogError("Handler not found for packet type: {PacketType}", packetType);
            return false;
        }

        return packetType switch
        {
            PacketType.ChatMessage => await handler(session, payload),
            _ => false,
        };
    }

    private async Task<bool> HandleChatMessageAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        _logger.LogDebug("Chat message received from session: {SessionId}", session.SessionId);
        //_logger.LogDebug("Chat payload raw data: {HexDump}, Length: {Length}", PacketHelper.ToHexDump(payload), payload.Length);

        if (!_managerContext.UserManager.TryGetUserBySession(session.SessionId, out var user))
        {
            _logger.LogError("User not found: {SessionId}", session.SessionId);
            return false;
        }

        var chatMessage = ChatMessage.Read(payload).WithSender(user!.Name);
        if (chatMessage == null)
        {
            _logger.LogError("Failed to read chat message: {SessionId}", session.SessionId);
            return false;
        }

        _logger.LogDebug("Chat message: {Sender} {Message}", chatMessage.Sender, chatMessage.Message);

        await _chatService.SendToAll(chatMessage);
        return true;
    }
}
