using Common.Network.Session;

namespace Common.Network.Handler;

public interface IPacketHandler
{
    MessageType Type { get; }
    Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}
