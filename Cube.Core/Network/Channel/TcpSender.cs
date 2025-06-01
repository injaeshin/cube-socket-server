using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;

namespace Cube.Core.Network;

public class TcpSender : SendChannel<TcpSendContext>
{
    private readonly IPoolHandler<SocketAsyncEventArgs> _poolHandler;
    private readonly INetworkService _networkService;

    public TcpSender(ILoggerFactory loggerFactory, IPoolHandler<SocketAsyncEventArgs> poolHandler, INetworkService networkService) : base(loggerFactory)
    {
        _poolHandler = poolHandler;
        _networkService = networkService;
    }

    protected override async Task OnProcessAsync(TcpSendContext ctx)
    {
        if (ctx.Socket is null)
        {
            _logger.LogError("[TcpSendChannel] Socket is null for session {SessionId}", ctx.SessionId);
            ctx.Return();
            return;
        }

        var saea = RentSocketAsyncEventArgs();
        saea.RemoteEndPoint = ctx.Socket.RemoteEndPoint;
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
            ReturnSocketAsyncEventArgs(saea);
        }

        await Task.CompletedTask;
    }

    protected override void OnSendCompletedAsync(object? sender, SocketAsyncEventArgs e)
    {
        _logger.LogDebug("[TcpSendChannel] Send completed {BytesTransferred}", e.BytesTransferred);

        //if (e.UserToken is TcpSendContext ctx)
        //{
        //    ctx.Return();
        //}
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
