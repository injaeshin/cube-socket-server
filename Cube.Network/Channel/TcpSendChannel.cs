using Microsoft.Extensions.Logging;

using Cube.Network.Context;
using Cube.Network.Pool;
using System.Net.Sockets;

namespace Cube.Network.Channel;

public class TcpSendChannel : SendChannel<TcpSendContext>
{
    public TcpSendChannel(ILoggerFactory loggerFactory, PoolEvent poolEvent) : base(loggerFactory, poolEvent) { }

    protected override async Task OnProcessAsync(TcpSendContext ctx)
    {
        var saea = RentSocketAsyncEventArgs();
        saea.SetBuffer(ctx.Data);
        saea.UserToken = ctx;

        try
        {
            if (!ctx.Socket.SendToAsync(saea))
            {
                OnSendCompletedAsync(null, saea);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TcpSendChannel] Error sending packet");
        }
        finally
        {
            ctx.Return();
        }

        await Task.CompletedTask;
    }

    protected override void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e)
    {
        if (e.UserToken is TcpSendContext ctx)
        {
            ctx.OnSendCompleted?.Invoke(ctx);
        }

        ReturnSocketAsyncEventArgs(e);
    }
}
