using Cube.Common.Shared;
using Cube.Core.Handler;
using Cube.Core.Sessions;

namespace Cube.Core.Dispatcher;

public interface IPacketDispatcher
{
    bool TryGetHandler(PacketType type, out IPacketHandler handler);
    Task<bool> DispatchAsync(ISession session, PacketType type, ReadOnlyMemory<byte> payload);

    bool IsRegistered(PacketType type);
    void Register(PacketType type, IPacketHandler handler);
}