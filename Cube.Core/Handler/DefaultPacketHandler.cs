using Cube.Common.Shared;
using Cube.Core.Sessions;

namespace Cube.Core.Handler;

public class DefaultPacketHandler : IPacketHandler
{
    public PacketType Type => PacketType.None;

    public Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
