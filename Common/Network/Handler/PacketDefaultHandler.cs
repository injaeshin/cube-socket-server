using Common.Network.Session;

namespace Common.Network.Handler;

public class PacketDefaultHandler : IPacketHandler
{
    public MessageType Type => MessageType.None;

    public Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        return Task.FromResult(false);
    }
}
