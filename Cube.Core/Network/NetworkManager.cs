using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Network;
using Cube.Core.Pool;
using Cube.Core.Router;
using Cube.Packet.Builder;
using Cube.Core.Settings;
using Cube.Packet;

namespace Cube.Core;

public interface INetworkManager
{
    void Run(int tcpPort, int udpPort = 0);
    void Close();

    IPoolHandler<SocketAsyncEventArgs> GetEventArgsPoolHandler();
}

public class NetworkManager : INetworkManager
{
    private readonly ILogger _logger;
    private readonly IFunctionRouter _functionRouter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NetworkConfig _networkConfig;

    private IResourceManager _resourceManager = null!;

    private TcpConnectionPool _tcpConnectionPool = null!;
    private UdpConnectionPool? _udpConnectionPool;

    private TcpAcceptor _tcpAcceptor = null!;
    private UdpReceiver? _udpReceiver = null;

    private bool _running = false;

    public NetworkManager(ILoggerFactory loggerFactory, IResourceManager resourceManager, IFunctionRouter functionRouter, ISettingsService settingsService)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<NetworkManager>();
        _resourceManager = resourceManager;
        _networkConfig = settingsService.Network;

        _functionRouter = functionRouter;
        _functionRouter.AddFunc<ClientAcceptedCmd, Task>(cmd => OnClientAcceptedAsync(cmd.Socket));
        _functionRouter.AddFunc<ClientDatagramReceivedCmd, Task>(cmd => OnClientDatagramReceivedAsync(cmd.Context));
    }

    public void Run(int tcpPort, int udpPort = 0)
    {
        if (_running) throw new InvalidOperationException("NetworkManager is already running");
        if (tcpPort <= 0) throw new InvalidOperationException("TCP Port is not set");

        var shouldUdp = udpPort > 0;
        CreateTransport(shouldUdp);

        if (_tcpAcceptor == null) throw new InvalidOperationException("TCP Acceptor is not created");
        _ = _tcpAcceptor.RunAsync(tcpPort, _networkConfig.ListenBacklog);

        if (shouldUdp)
        {
            if (_udpReceiver == null) throw new InvalidOperationException("UDP Receiver is not created");
            _ = _udpReceiver.RunAsync(udpPort);
        }

        _running = true;
    }

    private void CreateTransport(bool shouldUdp = false)
    {
        var tcpConnPool = _resourceManager.GetResource(ResourceKey.TcpConnectionPool);
        _tcpConnectionPool = tcpConnPool as TcpConnectionPool ?? throw new InvalidOperationException("TcpConnectionPool is not created");

        if (!CreateTcpAcceptor())
        {
            throw new InvalidOperationException("TcpAcceptor is not created");
        }

        if (shouldUdp)
        {
            var udpConnPool = _resourceManager.GetResource(ResourceKey.UdpConnectionPool);
            _udpConnectionPool = udpConnPool as UdpConnectionPool ?? throw new InvalidOperationException("UdpConnectionPool is not created");

            if (!CreateUdpReceiver())
            {
                throw new InvalidOperationException("UdpReceiver is not created");
            }
        }
    }

    private bool CreateTcpAcceptor()
    {
        if (_tcpAcceptor != null)
        {
            _logger.LogWarning("TCP Acceptor is already created");
            return false;
        }

        _tcpAcceptor = new TcpAcceptor(_loggerFactory, _functionRouter, GetEventArgsPoolHandler());
        return true;
    }

    private bool CreateUdpReceiver()
    {
        if (_udpReceiver != null)
        {
            _logger.LogWarning("UDP Receiver is already created");
            return false;
        }

        _udpReceiver = new UdpReceiver(_loggerFactory, _functionRouter, GetEventArgsPoolHandler());
        return true;
    }

    private async Task OnClientAcceptedAsync(Socket socket)
    {
        if (!_running) return;

        try
        {
            var conn = _tcpConnectionPool.Rent(socket) ?? throw new InvalidOperationException("Transport is not created");
            if (!_functionRouter.InvokeFunc<TcpConnectedCmd, bool>(new TcpConnectedCmd(conn)))
            {
                _logger.LogError("Session 생성 실패");
                _tcpConnectionPool.Return(conn);
                return;
            }

            _logger.LogDebug("TCP 세션 획득: {EndPoint}", socket.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP 세션 획득 실패");
        }

        await Task.CompletedTask;
    }

    private async Task OnClientDatagramReceivedAsync(UdpReceivedContext ctx)
    {
        if (!_running) return;
        if (_udpConnectionPool == null) throw new InvalidOperationException("UDP ConnectionPool is not created");

        await SendAckAsync(ctx);

        try
        {
            switch (ctx.PacketType)
            {
                case PacketType.Ack:
                    _logger.LogDebug("UDP Ack 수신: {EndPoint}, sessionId: {SessionId}", ctx.RemoteEndPoint, ctx.SessionId);
                    _functionRouter.InvokeAction<UdpReceivedAckCmd>(new UdpReceivedAckCmd(ctx.SessionId, ctx.Ack));
                    break;

                case PacketType.KnockKnock:
                    var udpConnection = _udpConnectionPool.Rent(ctx.RemoteEndPoint) ?? throw new InvalidOperationException("UDP Endpoint is not created");
                    if (!_functionRouter.InvokeFunc<UdpConnectedCmd, bool>(new UdpConnectedCmd(ctx.SessionId, ctx.Sequence, udpConnection)))
                    {
                        _logger.LogError("Session 생성 실패");
                        _udpConnectionPool.Return(udpConnection);
                        ctx.Return();
                        return;
                    }
                    _logger.LogDebug("UDP 세션 획득: {EndPoint}, sessionId: {SessionId}", ctx.RemoteEndPoint, ctx.SessionId);
                    break;

                default:
                    _logger.LogDebug("UDP 데이터 수신: {EndPoint}, sessionId: {SessionId}", ctx.RemoteEndPoint, ctx.SessionId);
                    _functionRouter.InvokeAction<UdpReceivedCmd>(new UdpReceivedCmd(ctx));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP 세션 획득 실패");
        }

        await Task.CompletedTask;
    }

    private async Task SendAckAsync(UdpReceivedContext ctx)
    {
        if (ctx.PacketType == PacketType.Ack) return;

        var (data, rentedBuffer) = new PacketWriter(PacketType.Ack).ToUdpPacket();
        data.SetUdpAckHeader(ctx.SessionId, ctx.Sequence);

        var sendCtx = new UdpSendContext(ctx.SessionId, data, rentedBuffer, ctx.RemoteEndPoint, 0);
        await _functionRouter.InvokeFunc<UdpSendCmd, Task>(new UdpSendCmd(sendCtx));
    }

    public void Close()
    {
        CloseAsync().Wait();
    }

    private async Task CloseAsync()
    {
        if (!_running) throw new InvalidOperationException("NetworkManager is closed");
        _running = false;

        _tcpAcceptor?.Stop();
        _udpReceiver?.Stop();

        await _resourceManager.ShutdownAsync(TimeSpan.FromSeconds(1));
    }

    public IPoolHandler<SocketAsyncEventArgs> GetEventArgsPoolHandler()
    {
        return _resourceManager.GetPoolHandler(ResourceKey.SocketAsyncEventArgsPool);
    }
}

