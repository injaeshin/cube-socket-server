using System.Net.Sockets;

namespace Common.Network.Transport;
public interface ITransport : IDisposable
{
    bool IsConnectionAlive();
    Task SendAsync(ReadOnlyMemory<byte> payload);

    void Run();
    void Close();
    void Reset();
    void BindSocket(Socket socket, SocketAsyncEventArgs receiveArgs);
    void BindNotify(ITransportNotify notify);
}

public interface ITransportNotify
{
    void OnNotifyConnected();
    void OnNotifyDisconnected(bool isGraceful);
    void OnNotifyError(Exception exception);
    Task OnNotifyReceived(MessageType packetType, ReadOnlyMemory<byte> packet, byte[]? rentedBuffer);
}