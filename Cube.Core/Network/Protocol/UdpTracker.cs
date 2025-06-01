using System.Collections.Concurrent;

namespace Cube.Core.Network;

public class UdpTracker
{
    private ushort _expectedSequence = 0;
    private ushort _currentSequence = 0;
    private readonly SortedDictionary<ushort, UdpReceivedContext> _outOfOrderPackets = new();
    private readonly ConcurrentDictionary<ushort, UnackedContext> _unackedPackets = new();

    private Func<UdpSendContext, Task>? _onSend;
    private Func<UdpReceivedContext, Task>? _onReceived;

    public void Run(Func<UdpSendContext, Task> onSend, Func<UdpReceivedContext, Task> onReceived)
    {
        _onSend = onSend;
        _onReceived = onReceived;
    }

    public void Clear()
    {
        _expectedSequence = 0;
        _currentSequence = 0;
        _onSend = null;
        _onReceived = null;

        foreach (var (_, entry) in _unackedPackets)
        {
            entry.SendContext.Return();
        }

        foreach (var (_, ctx) in _outOfOrderPackets)
        {
            ctx.Return();
        }

        _unackedPackets.Clear();
        _outOfOrderPackets.Clear();
    }

    public ushort NextSequence() => ++_currentSequence;
    public void InitExpectedSeqence(ushort seq) => _expectedSequence = (ushort)(seq + 1);

    public void Track(UdpSendContext sendContext)
    {
        if (_unackedPackets.TryGetValue(sendContext.Sequence, out var unacked))
        {
            unacked.LastSent = DateTime.UtcNow;
            return;
        }

        _unackedPackets.TryAdd(sendContext.Sequence, new UnackedContext(sendContext, DateTime.UtcNow));
    }

    public void Acknowledge(ushort seq)
    {
        if (_unackedPackets.TryRemove(seq, out var ctx))
        {
            ctx.SendContext.Return();
        }
    }

    public void ResendUnacked(DateTime now)
    {
        foreach (var (_, entry) in _unackedPackets)
        {
            if ((now - entry.LastSent).TotalMilliseconds > CoreConsts.RESEND_INTERVAL_MS)
            {
                entry.LastSent = now;
                _ = _onSend?.Invoke(entry.SendContext);
            }
        }
    }

    public void UpdateReceived(UdpReceivedContext ctx)
    {
        if (IsDuplicateOrOld(ctx.Sequence)) return;

        if (ctx.Sequence == _expectedSequence)
        {
            _ = _onReceived?.Invoke(ctx);
            _expectedSequence++;
            FlushOutOfOrderPackets();
        }
        else
        {
            _outOfOrderPackets.TryAdd(ctx.Sequence, ctx);
        }
    }

    private bool IsDuplicateOrOld(ushort seq) => (ushort)(seq - _expectedSequence) > 0x8000;

    private void FlushOutOfOrderPackets()
    {
        while (_outOfOrderPackets.TryGetValue(_expectedSequence, out var nextPayload))
        {
            _outOfOrderPackets.Remove(_expectedSequence);
            _ = _onReceived?.Invoke(nextPayload);
            _expectedSequence++;
        }
    }
}
