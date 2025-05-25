using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Network.Pool;
using Cube.Network.Channel;
using Cube.Network.Context;

namespace Cube.Network.Transport;

public class UdpSender : IDisposable
{
    private readonly ILogger _logger;
    private readonly Socket _socket;
    private readonly UdpSendChannel _sendChannel;
    private readonly TransportUdpSendEvent _sendEvent;
    private bool _isClosed = false;
    private bool _disposed = false;

    public UdpSender(ILoggerFactory loggerFactory, PoolEvent poolEvent, TransportUdpSendEvent sendEvent)
    {
        _logger = loggerFactory.CreateLogger<UdpSender>();
        _sendEvent = sendEvent;

        _sendChannel = new UdpSendChannel(loggerFactory, poolEvent);

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
    }

    public async Task Run()
    {
        _isClosed = false;
        await Task.CompletedTask;
    }

    public async Task SendAsync(UdpSendContext context)
    {
        if (_isClosed) return;

        context.Socket = _socket;
        context.OnUdpPreDatagramSend = _sendEvent.OnUdpPresetSend;
        await _sendChannel.EnqueueAsync(context);
    }

    ~UdpSender()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _isClosed = true;
            _sendChannel.Close();
        }

        if (_socket != null)
        {
            _socket.Close();
            _socket.Dispose();
        }
    }
}

