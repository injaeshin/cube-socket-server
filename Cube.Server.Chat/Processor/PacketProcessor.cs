using Microsoft.Extensions.Logging;
using Cube.Core;
using Cube.Core.Sessions;
using Cube.Packet;
using Cube.Core.Network;
using Cube.Server.Chat.Service;

namespace Cube.Server.Chat.Processor;

public interface IPacketProcessor
{
    Task ExecuteAsync(ReceivedContext context);
    Task OnReceivedAsync(ReceivedContext context);

    Task OnSessionClosed(string sessionId);
}

public class PacketProcessor : IPacketProcessor
{
    private readonly ILogger _logger;
    private readonly ProcessChannel _processChannel;
    private readonly IServiceContext _managerContext;
    private readonly Dictionary<PacketDomain, IPacketDispatcher> _packetProcessors;

    public PacketProcessor(ILoggerFactory loggerFactory, IObjectFactoryHelper objectFactory, IServiceContext managerContext)
    {
        _logger = loggerFactory.CreateLogger<PacketProcessor>();
        _processChannel = new ProcessChannel(loggerFactory, ExecuteAsync);
        _managerContext = managerContext;
        _packetProcessors = new()
        {
            { PacketDomain.Auth, objectFactory.Create<AuthService>() },
            { PacketDomain.Chat, objectFactory.Create<ChatService>() },
            // { PacketDomain.Channel, objectFactory.Create<ChannelProcessor>() },
        };
    }

    public async Task OnReceivedAsync(ReceivedContext context)
    {
        await _processChannel.EnqueueAsync(context);
    }

    public async Task ExecuteAsync(ReceivedContext context)
    {
        if (!PacketHelper.TryGetPacketType(context.Payload, out var type))
        {
            _logger.LogError("Invalid packet type / {SessionId}", context.SessionId);
            Close(SocketDisconnect.InvalidPacket, context);
            return;
        }

        var packetDomain = (PacketDomain)type;
        if (!_packetProcessors.TryGetValue(packetDomain, out var processor))
        {
            _logger.LogError("Processor not found: {PacketDomain}", packetDomain);
            Close(SocketDisconnect.InvalidPacket, context);
            return;
        }

        await processor.ProcessAsync(context.SessionId, type, context.Payload);
        context.Return();
    }

    public async Task OnSessionClosed(string sessionId)
    {
        if (!_packetProcessors.TryGetValue(PacketDomain.Auth, out var processor))
        {
            _logger.LogError("Processor not found: {PacketDomain}", PacketDomain.Auth);
            return;
        }

        await processor.ProcessAsync(sessionId, PacketType.Logout, ReadOnlyMemory<byte>.Empty);
    }

    private void Close(SocketDisconnect reason, ReceivedContext context)
    {
        if (_managerContext.SessionManager.TryGetSession(context.SessionId, out var session))
        {
            session!.Close(new SessionClose(reason));
        }
        else
        {
            _logger.LogError("Session not found: {SessionId}", context.SessionId);
        }

        context.Return();
    }
}

