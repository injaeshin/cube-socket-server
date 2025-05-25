using System;
using Cube.Network.Channel;
using Cube.Network.Context;
using Cube.Network.Pool;
using Microsoft.Extensions.Logging;

namespace Cube.Network.Transport;

public class TcpSender
{
    private readonly ILogger _logger;
    private readonly TcpSendChannel _sendChannel;
    private bool _isClosed = false;

    private readonly Func<TcpSendContext, Task> _onSendCompleted;

    public TcpSender(ILoggerFactory loggerFactory, PoolEvent poolEvent, Func<TcpSendContext, Task> onSendCompleted)
    {
        _logger = loggerFactory.CreateLogger<TcpSender>();
        _sendChannel = new TcpSendChannel(loggerFactory, poolEvent);
        _onSendCompleted = onSendCompleted;
    }

    public async Task Run()
    {
        _isClosed = false;
        await Task.CompletedTask;
    }

    public void Stop()
    {
        _isClosed = true;
        _sendChannel.Close();
    }

    public async Task SendAsync(TcpSendContext context)
    {
        if (_isClosed) return;

        context.OnSendCompleted = _onSendCompleted;
        await _sendChannel.EnqueueAsync(context);
    }

}
