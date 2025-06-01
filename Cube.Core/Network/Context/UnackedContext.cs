
namespace Cube.Core.Network;

public class UnackedContext(UdpSendContext sendContext, DateTime lastSent)
{
    public UdpSendContext SendContext { get; } = sendContext;
    public DateTime LastSent { get; set; } = lastSent;
}