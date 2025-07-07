using Microsoft.Extensions.Logging;
using System.Net;
using Cube.Core.Pool;

namespace Cube.Core.Network;

public interface IUdpConnection : IDisposable
{
    void BindEndPoint(EndPoint remoteEndPoint);
    void BindNotify(INotifySession notify);
    void Run();
    void Close();

    EndPoint GetEndPoint();
    ushort NextSequence();
    void InitExpectedSeqence(ushort seq);

    void UpdateReceived(UdpReceivedContext ctx);
    void Track(UdpSendContext sendContext);
    void Acknowledge(ushort seq);
    void ResendUnacked(DateTime now);
}

public class UdpConnection : IUdpConnection
{
    private readonly ILogger _logger;
    private readonly UdpTracker _sequenceTracker;
    private readonly IUdpConnectionPool _udpConnectionPool;

    private EndPoint _remoteEndPoint = null!;
    private INotifySession _notify = null!;

    private bool _closed = false;

    public UdpConnection(ILoggerFactory loggerFactory, IUdpConnectionPool udpConnectionPool, int resendIntervalMs)
    {
        _logger = loggerFactory.CreateLogger<UdpConnection>();
        _udpConnectionPool = udpConnectionPool;
        _sequenceTracker = new UdpTracker(resendIntervalMs);
    }

    public void BindEndPoint(EndPoint remoteEndPoint)
    {
        _remoteEndPoint = remoteEndPoint;
    }

    public void BindNotify(INotifySession notify)
    {
        _notify = notify;
    }

    public void Run()
    {
        _closed = false;
        _sequenceTracker.Run(OnSend, OnReceived);
    }

    public EndPoint GetEndPoint() => _remoteEndPoint;

    public ushort NextSequence() => _sequenceTracker.NextSequence();

    public void InitExpectedSeqence(ushort seq) => _sequenceTracker.InitExpectedSeqence(seq);

    public void UpdateReceived(UdpReceivedContext ctx) => _sequenceTracker.UpdateReceived(ctx);

    public void Track(UdpSendContext sendContext) => _sequenceTracker.Track(sendContext);

    public void Acknowledge(ushort seq) => _sequenceTracker.Acknowledge(seq);

    public void ResendUnacked(DateTime now) => _sequenceTracker.ResendUnacked(now);

    private Task OnSend(UdpSendContext sendContext) => _notify.OnNotifyUdpSend(sendContext);

    private Task OnReceived(UdpReceivedContext ctx) => _notify.OnNotifyReceived(ctx);

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _notify = null!;
        _sequenceTracker.Clear();
        _udpConnectionPool.Return(this);
    }

    public void Dispose()
    {
        if (_closed) return;
        Close();
    }
}