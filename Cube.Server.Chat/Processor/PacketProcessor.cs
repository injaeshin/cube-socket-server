using Microsoft.Extensions.Logging;
using Cube.Core.Network;
using Cube.Core.Execution;
using Cube.Core;
using Cube.Packet;

namespace Cube.Server.Chat.Processor;

public interface IPacketProcessor : IProcessor { }

public class PacketProcessor : Core.Execution.Processor, IPacketProcessor
{
    private readonly ILogger _logger;
    private readonly IManagerContext _managerContext;
    private readonly Dictionary<PacketDomain, IPacketDispatcher> _packetProcessors;

    public PacketProcessor(ILoggerFactory loggerFactory, IObjectFactoryHelper objectFactory, IManagerContext managerContext) : base(loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PacketProcessor>();
        _managerContext = managerContext;
        _packetProcessors = new()
        {
            { PacketDomain.Auth, objectFactory.Create<AuthDispatcher>() },
            { PacketDomain.Chat, objectFactory.Create<ChatDispatcher>() },
            // { PacketDomain.Channel, objectFactory.Create<ChannelProcessor>() },
        };
    }

    private static bool IsValidatePacketType(PacketType packetType)
    {
        return Enum.IsDefined(typeof(PacketType), packetType);
    }

    public override async Task ExecuteAsync(ReceivedContext ctx)
    {
        if (!IsValidatePacketType(ctx.PacketType))
        {
            _logger.LogError("Invalid packet type: {PacketType}", ctx.PacketType);
            Kick(ErrorType.InvalidPacket, ctx);
            return;
        }

        var packetDomain = ctx.PacketType.GetPacketDomain();
        if (!_packetProcessors.TryGetValue(packetDomain, out var processor))
        {
            _logger.LogError("Processor not found: {PacketDomain}", packetDomain);
            Kick(ErrorType.InvalidPacket, ctx);
            return;
        }

        if (!await processor.ProcessAsync(ctx.SessionId, ctx.PacketType, ctx.Payload))
        {
            _logger.LogError("Process failed: {SessionId}, {PacketType}", ctx.SessionId, ctx.PacketType);
            Kick(ErrorType.ProcessFailed, ctx);
            return;
        }

        ctx.Return();
    }

    private void Kick(ErrorType reason, ReceivedContext ctx)
    {
        _managerContext.SessionManager.Kick(ctx.SessionId, reason);
        ctx.Return();
    }
}

