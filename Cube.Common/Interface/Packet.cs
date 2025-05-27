
namespace Cube.Common.Interface;

public interface IPacketFactory
{
    public (Memory<byte> data, byte[]? rentedBuffer) CreatePingPacket();
    public (Memory<byte> data, byte[]? rentedBuffer) CreateChatMessagePacket(string sender, string message);
}