using Cube.Core;
using Cube.Core.Execution;
using Cube.Packet;

namespace Cube.Server.Chat.Processor;

internal abstract class Dispatcher(IManagerContext managerContext) : IPacketDispatcher
{
    protected readonly IManagerContext _managerContext = managerContext;

    protected Dictionary<PacketType, Func<ISession, ReadOnlyMemory<byte>, Task<bool>>> _handlers = [];

    public abstract Task<bool> ProcessAsync(string sessionId, PacketType packetType, ReadOnlyMemory<byte> payload);
}
