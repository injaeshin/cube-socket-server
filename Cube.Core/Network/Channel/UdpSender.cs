using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;

namespace Cube.Core.Network;

public class UdpSender : SendChannel<UdpSendContext>
{
    private readonly Socket _socket;
    private readonly IPoolHandler<SocketAsyncEventArgs> _poolHandler;
    private readonly INetworkService _networkService;

    public UdpSender(ILoggerFactory loggerFactory, IPoolHandler<SocketAsyncEventArgs> poolHandler, INetworkService networkService) : base(loggerFactory)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _poolHandler = poolHandler;
        _networkService = networkService;
    }

    protected override async Task OnProcessAsync(UdpSendContext ctx)
    {
        var saea = RentSocketAsyncEventArgs();
        saea.RemoteEndPoint = ctx.RemoteEndPoint;
        saea.SetBuffer(ctx.Data);
        saea.UserToken = ctx;

        try
        {
            if (ctx.ShouldTrack() && !_networkService.OnUdpTrackSent(ctx))
            {
                _logger.LogError("[UdpSendChannel] OnUdpTrackSent returned false");
                ReturnSocketAsyncEventArgs(saea);
                ctx.Return();
                return;
            }

            if (!_socket.SendToAsync(saea))
            {
                OnSendCompletedAsync(null, saea);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[UdpSendChannel] Error sending packet");
            ReturnSocketAsyncEventArgs(saea);
        }

        await Task.CompletedTask;
    }

    protected override void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e)
    {
        UdpSendContext ctx = e.UserToken as UdpSendContext ?? throw new InvalidOperationException("UserToken is not UdpSendContext");
        if (ctx == null)
        {
            _logger.LogError("[UdpSendChannel] UserToken is null");
            ReturnSocketAsyncEventArgs(e);
            return;
        }

        _logger.LogDebug("[UdpSendChannel] {ack} Send completed {BytesTransferred}", !ctx.ShouldTrack() ? "Ack" : string.Empty, e.BytesTransferred);

        if (!ctx.ShouldTrack())
        {
            ctx.Return();
        }

        ReturnSocketAsyncEventArgs(e);
    }

    private SocketAsyncEventArgs RentSocketAsyncEventArgs()
    {
        var e = _poolHandler.RentWithoutBuffer() ?? throw new Exception("SocketAsyncEventArgs is null");
        e.Completed += OnSendCompletedAsync;
        e.SetBuffer(Memory<byte>.Empty);
        return e;
    }

    private void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs e)
    {
        e.Completed -= OnSendCompletedAsync;
        e.SetBuffer(Memory<byte>.Empty);
        _poolHandler.Return(e);
    }
}
