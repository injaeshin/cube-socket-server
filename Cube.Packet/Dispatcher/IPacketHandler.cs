using Cube.Common.Interface;

namespace Cube.Packet;

// 기본 인터페이스
public interface IPacketHandler
{
    PacketType Type { get; }
    Task<bool> HandleAsync(ISession session);
}

// 페이로드가 필요한 핸들러를 위한 인터페이스
public interface IPayloadPacketHandler : IPacketHandler
{
    Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}

// 기본 구현을 제공하는 추상 클래스
public abstract class PacketHandlerBase : IPacketHandler
{
    public abstract PacketType Type { get; }

    public virtual Task<bool> HandleAsync(ISession session)
    {
        return Task.FromResult(true);
    }
}

// 페이로드가 필요한 핸들러를 위한 추상 클래스
public abstract class PayloadPacketHandlerBase : PacketHandlerBase, IPayloadPacketHandler
{
    public abstract Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}
