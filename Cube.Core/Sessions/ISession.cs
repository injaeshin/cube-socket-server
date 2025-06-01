using System.Net;
using Cube.Core.Network;
using Cube.Packet;

namespace Cube.Core;

public interface ISession
{
    string SessionId { get; }

    bool IsConnected { get; }
    bool IsDisconnected { get; }
    bool IsAuthenticated { get; }
    void SetAuthenticated();

    Task SendAsync(Memory<byte> data, byte[]? rentedBuffer, TransportType transportType = TransportType.Tcp);
    void Kick(ErrorType reason);
}

public interface ICoreSession : ISession
{
    void Bind(ITcpConnection conn);
    void Bind(IUdpConnection conn);

    ITcpConnection? TcpConnection { get; }
    IUdpConnection? UdpConnection { get; }


    void CloseUdpConnection();
}

public interface INotifySession
{
    string SessionId { get; }

    void OnNotifyConnected(TransportType transportType);
    void OnNotifyDisconnected(TransportType transportType, bool isGraceful);
    void OnNotifyError(TransportType transportType, Exception exception);

    Task OnNotifyUdpSend(UdpSendContext context);
    Task OnNotifyReceived(ReceivedContext context);
}
