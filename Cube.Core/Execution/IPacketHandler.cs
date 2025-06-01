using Cube.Packet;

namespace Cube.Core.Execution;

// 기본 인터페이스
public interface IPacketHandler
{
    PacketType Type { get; }
    Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}
