using System.Net.Sockets;

namespace Cube.Core.Network;

// public class TcpConnectionHandler
// {
//     public required Action<ITcpConnection> OnReturnConnection;
//     public required Action<PacketBuffer> OnReturnBuffer;
//     public required Action<SocketAsyncEventArgs> OnReturnEventArgs;
// }

public class UdpReturnHandler
{
    public required Action<IUdpConnection> OnReturnConnection;
}

public class UdpConnectionHandler
{
    public required Func<UdpSendContext, Task> OnUdpSend;
    public required Func<string, ushort, ReadOnlyMemory<byte>, bool> OnUdpSendTrack;
    public required Func<UdpReceivedContext, Task> OnUdpDatagramReceived;
}
