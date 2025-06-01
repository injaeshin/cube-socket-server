using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Network;
using Cube.Core.Pool;
using Cube.Core.Router;
using Cube.Core.Execution;
using Microsoft.Extensions.Hosting;

namespace Cube.Core;

public interface INetworkService
{
    Task OnTcpSendAsync(TcpSendContext context);
    Task OnUdpSendAsync(UdpSendContext context);
    //void OnSendCompleted(SendContext context);
    //void OnSendCompleted(UdpSendContext context);
    Task OnReceivedEnqueueAsync(ReceivedContext context);
    bool OnUdpTrackSent(UdpSendContext context);
}

public class NetworkService : IHostedService, INetworkService
{
    private readonly ILogger _logger;
    private readonly IProcessor _packetProcessor;
    private readonly IFunctionRouter _functionRouter;

    private readonly TcpSender _tcpSender;
    private readonly UdpSender _udpSender;
    private readonly ProcessChannel<ReceivedContext> _processChannel;

    private bool _running = false;

    public NetworkService(ILoggerFactory loggerFactory, IPoolHandler<SocketAsyncEventArgs> poolEvent, IFunctionRouter functionRouter, IProcessor packetProcessor)
    {
        _logger = loggerFactory.CreateLogger<NetworkService>();
        _packetProcessor = packetProcessor;
        _functionRouter = functionRouter;

        _tcpSender = new TcpSender(loggerFactory, poolEvent, this);
        _udpSender = new UdpSender(loggerFactory, poolEvent, this);
        _processChannel = new ProcessChannel<ReceivedContext>(loggerFactory, OnReceivedProcessAsync);

        _functionRouter.AddFunc<ReceivedEnqueueCmd, Task>(cmd => OnReceivedEnqueueAsync(cmd.Context));
        _functionRouter.AddFunc<TcpSendCmd, Task>(cmd => OnTcpSendAsync(cmd.Context));
        _functionRouter.AddFunc<UdpSendCmd, Task>(cmd => OnUdpSendAsync(cmd.Context));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;

        _logger.LogDebug("Started NetworkService...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_running) throw new InvalidOperationException("NetworkService is not running");
        _running = false;

        _tcpSender.Close();
        _udpSender.Close();
        _processChannel.Close();

        _logger.LogDebug("Stopped NetworkService...");
        return Task.CompletedTask;
    }

    public async Task OnTcpSendAsync(TcpSendContext context)
    {
        if (!_running)
        {
            _logger.LogWarning("NetworkService is not running");
            return;
        }

        await _tcpSender.EnqueueAsync(context);
    }

    //public void OnSendCompleted(SendContext context)
    //{
    //    throw new NotImplementedException();
    //}

    public async Task OnUdpSendAsync(UdpSendContext context)
    {
        if (!_running)
        {
            _logger.LogWarning("NetworkService is not running");
            return;
        }

        await _udpSender.EnqueueAsync(context);
    }

    public bool OnUdpTrackSent(UdpSendContext context)
    {
        if (!_running)
        {
            _logger.LogWarning("NetworkService is not running");
            return false;
        }

        _functionRouter.InvokeAction<UdpTrackSentCmd>(new UdpTrackSentCmd(context));

        return true;
    }

    //public void OnSendCompleted(UdpSendContext context)
    //{
    //    //throw new NotImplementedException();
    //}

    public async Task OnReceivedEnqueueAsync(ReceivedContext context)
    {
        if (!_running)
        {
            _logger.LogWarning("NetworkService is not running");
            return;
        }

        await _processChannel.EnqueueAsync(context);
    }

    private async Task OnReceivedProcessAsync(ReceivedContext context)
    {
        await _packetProcessor.ExecuteAsync(context);
        //await _functionRouter.InvokeFunc<ReceivedExecuteCmd<ReceivedContext>, Task>(new ReceivedExecuteCmd<ReceivedContext>(context));
    }
}
