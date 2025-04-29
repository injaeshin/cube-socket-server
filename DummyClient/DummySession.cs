
using System.Net.Sockets;
using Common.Network;
using Common.Network.Packet;
using Common.Network.Session;
using Microsoft.Extensions.Logging;

namespace __DummyClient;

public interface IDummySession : ISession
{
    public Task ConnectAsync(string host, int port);
    public void Disconnect();

    public bool IsConnected { get; }
}

public class DummySession(ILoggerFactory logger, Func<ReceivedPacket, Task> onReceivedPacket) : Session(logger), IDummySession, IDisposable
{
    private Socket _socket = null!;
    private SocketAsyncEventArgs _receiveArgs = null!;
    private byte[] _buffer = new byte[NetConsts.BUFFER_SIZE];

    private readonly Func<ReceivedPacket, Task> _onReceivedPacket = onReceivedPacket;

    public async Task ConnectAsync(string host, int port)
    {
        _receiveArgs = new SocketAsyncEventArgs();
        _receiveArgs.SetBuffer(_buffer, 0, _buffer.Length);

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await _socket.ConnectAsync(host, port, CancellationToken.None);

        Run(_socket, _receiveArgs);
    }

    public void Disconnect()
    {
        Close();
    }

    public override async Task OnProcessReceivedPacketAsync(ReceivedPacket packet)
    {
        await _onReceivedPacket(packet);
    }

    public bool IsConnected => Socket.Connected;
}