using Cube.Common.Shared;
using Cube.Session;

namespace Cube.Handler;

public interface IPacketDispatcher
{
    bool TryGetHandler(PacketType type, out IPacketHandler handler);
    Task<bool> DispatchAsync(INetSession session, PacketType type, ReadOnlyMemory<byte> payload);

    bool IsRegistered(PacketType type);
    void Register(PacketType type, IPacketHandler handler);
}