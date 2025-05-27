using Microsoft.Extensions.Logging;
using Cube.Core.Pool;

namespace Cube.Core.Network;

public class TcpSender
{
    private readonly TcpSendChannel _sendChannel;
    private bool _isClosed = false;

    private readonly Func<TcpSendContext, Task> _onSendCompleted;

    public TcpSender(ILoggerFactory loggerFactory, PoolEvent poolEvent, Func<TcpSendContext, Task> onSendCompleted)
    {
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
