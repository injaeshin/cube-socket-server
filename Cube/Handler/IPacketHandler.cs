using Cube.Common.Shared;
using Cube.Session;

namespace Cube.Handler;

public interface IPacketHandler
{
    PacketType Type { get; }
    Task<bool> HandleAsync(INetSession session, ReadOnlyMemory<byte> payload);
}
