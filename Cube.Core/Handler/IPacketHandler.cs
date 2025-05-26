using Cube.Common.Shared;
using Cube.Core.Sessions;

namespace Cube.Core.Handler;

public interface IPacketHandler
{
    PacketType Type { get; }
    Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}
