using System.Net;
using System.Net.Sockets;

namespace Cube.Network.Transport;


public class TransportUdpSendEvent
{
    public required Func<string, ushort, ReadOnlyMemory<byte>, bool> OnUdpPresetSend;
}

public class TransportUdpReceiveEvent
{
    public required Action<EndPoint, string> OnUdpGreetingReceived;
    public required Action<EndPoint, ReadOnlyMemory<byte>> OnUdpDatagramReceived;
}

public class TransportReturnEvent
{
    public required Action<ITransport> OnReleaseTransport;
    public required Action<SocketAsyncEventArgs> OnReleaseReceiveArgs;
}