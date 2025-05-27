using System.Collections.Concurrent;
using System.Net;

namespace Cube.Core.Network;

public class RudpSendBuffer : IDisposable
{
    private ushort _currentSequence = 0;

    private readonly ConcurrentDictionary<ushort, SendEntry> _unackedPackets = new();
    private readonly Func<EndPoint, ushort, ReadOnlyMemory<byte>, Task> _resendFunc;
    private readonly Func<EndPoint> _getEndPointFunc;

    public RudpSendBuffer(Func<EndPoint, ushort, ReadOnlyMemory<byte>, Task> resendFunc, Func<EndPoint> getEndPointFunc)
    {
        _resendFunc = resendFunc;
        _getEndPointFunc = getEndPointFunc;
    }

    public ushort NextSequence => _currentSequence++;

    public void Track(ushort seq, ReadOnlyMemory<byte> payload)
    {
        _unackedPackets.TryAdd(seq, new SendEntry(payload, DateTime.UtcNow));
    }

    public void Acknowledge(ushort seq)
    {
        _unackedPackets.TryRemove(seq, out _);
    }

    public void ResendUnacked()
    {
        var now = DateTime.UtcNow;

        foreach (var (seq, entry) in _unackedPackets)
        {
            if ((now - entry.LastSent).TotalMilliseconds > 300)
            {
                entry.LastSent = now;
                _ = _resendFunc(_getEndPointFunc(), seq, entry.Data);
            }
        }
    }

    public void Reset()
    {
        _unackedPackets.Clear();
        _currentSequence = 0;
    }

    public void Dispose()
    {
        _unackedPackets.Clear();
    }

    public class SendEntry(ReadOnlyMemory<byte> data, DateTime lastSent)
    {
        public ReadOnlyMemory<byte> Data { get; } = data;
        public DateTime LastSent { get; set; } = lastSent;
    }
}
