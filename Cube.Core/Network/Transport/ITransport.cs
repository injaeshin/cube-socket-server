using System.Net.Sockets;
using Cube.Core.Sessions;

namespace Cube.Core.Network;

public interface ITransport : IDisposable
{
    bool IsConnectionAlive();
    void Run();
    void Close(bool isGraceful = false);
    void BindSocket(Socket socket, SocketAsyncEventArgs receiveArgs);
    void BindNotify(ISessionNotify notify);
    Socket GetSocket();
}

