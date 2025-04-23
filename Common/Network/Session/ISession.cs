using System.Net.Sockets;

using Common.Network.Packet;
using Common.Network.Transport;

namespace Common.Network.Session;
public interface ISession : IDisposable
{
    string SessionId { get; }
    bool IsConnectionAlive();

    void CreateSessionId();
    void ResetBuffer();

    void Run(Socket socket);
    void Close(DisconnectReason reason = DisconnectReason.ApplicationRequest);
    void ForceClose(DisconnectReason reason = DisconnectReason.SocketError);
    Task OnProcessReceivedAsync(ReceivedPacket packet);

    Task SendAsync(PacketType type, ReadOnlyMemory<byte> payload);
}