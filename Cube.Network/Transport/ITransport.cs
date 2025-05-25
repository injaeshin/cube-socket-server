using System.Net.Sockets;

namespace Cube.Network.Transport;
public interface ITransport : IDisposable
{
    bool IsConnectionAlive();
    void Run();
    void Close(bool isGraceful = false);
    void BindSocket(Socket socket, SocketAsyncEventArgs receiveArgs);
    void BindNotify(ITransportNotify notify);
    Socket GetSocket();
}

public interface ITransportNotify
{
    void OnNotifyConnected();
    void OnNotifyDisconnected(bool isGraceful);
    void OnNotifyError(Exception exception);

    Task OnNotifyReceived(ReadOnlyMemory<byte> payload, byte[]? rentedBuffer);
}