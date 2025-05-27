using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;

namespace Cube.Core.Network;

public class UdpSendChannel : SendChannel<UdpSendContext>
{
    public UdpSendChannel(ILoggerFactory loggerFactory, PoolEvent poolEvent) : base(loggerFactory, poolEvent) { }

    protected override async Task OnProcessAsync(UdpSendContext ctx)
    {
        var saea = RentSocketAsyncEventArgs();
        saea.RemoteEndPoint = ctx.RemoteEndPoint;
        saea.UserToken = ctx;
        saea.SetBuffer(ctx.Data);

        try
        {
            if (ctx.Socket == null)
            {
                _logger.LogError("[UdpSendChannel] Socket is null");
                return;
            }

            if (ctx.OnUdpPreDatagramSend == null)
            {
                _logger.LogError("[UdpSendChannel] OnUdpPreDatagramSend is null");
                return;
            }

            if (!ctx.OnUdpPreDatagramSend(ctx.SessionId, ctx.Sequence, ctx.Data))
            {
                _logger.LogError("[UdpSendChannel] OnUdpPreDatagramSend returned false");
                return;
            }

            if (!ctx.Socket.SendToAsync(saea))
            {
                OnSendCompletedAsync(null, saea);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[UdpSendChannel] Error sending packet");
        }
        finally
        {
            ctx.Return();
        }

        await Task.CompletedTask;
    }

    protected override void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e)
    {
        ReturnSocketAsyncEventArgs(e);
    }
}
