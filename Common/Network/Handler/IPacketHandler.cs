using System;
using Common.Network.Packet;
using Common.Network.Session;

namespace Common.Network.Handler;

public interface IPacketHandler
{
    PacketType PacketType { get; }
    Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload);
}
