using System.Net;
using System.Net.Sockets;

namespace Cube.Core.Network;

public class TransportUdpSendHandler
{
    public required Func<string, ushort, ReadOnlyMemory<byte>, bool> OnUdpPresetSend;
}

public class TransportUdpReceiveHandler
{
    public required Action<EndPoint, string> OnUdpGreetingReceived;
    public required Action<EndPoint, ReadOnlyMemory<byte>> OnUdpDatagramReceived;
}

public class TransportReturnHandler
{
    public required Action<ITransport> OnReleaseTransport;
    public required Action<SocketAsyncEventArgs> OnReleaseReceiveArgs;
}