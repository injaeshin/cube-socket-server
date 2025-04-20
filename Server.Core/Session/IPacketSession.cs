using System.Net.Sockets;

using Common.Protocol;

namespace Server.Core.Session;


public interface ISocketSession
{
    string SessionId { get; }
    bool IsConnectionAlive();

    void CreateSessionId();

    void Run(Socket socket);
    void Close(DisconnectReason reason = DisconnectReason.ApplicationRequest);
    void ForceClose(DisconnectReason reason = DisconnectReason.SocketError);
    Task OnProcessReceivedAsync(ReadOnlyMemory<byte> data);

    Task SendAsync(PacketType type, ReadOnlyMemory<byte> payload);
}