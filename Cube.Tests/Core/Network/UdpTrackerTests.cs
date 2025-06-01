using Cube.Core.Network;
using Cube.Packet;
using System.Net;

namespace Cube.Tests.Core.Network;

public class UdpTrackerTests
{
    [Fact]
    public async Task Track_And_Ack_Works_Correctly()
    {
        var tracker = new UdpTracker();
        bool onSendCalled = false;
        //bool onReceivedCalled = false;

        tracker.Run(
            ctx => { onSendCalled = true; return Task.CompletedTask; },
            ctx => { return Task.CompletedTask; }
        );

        // Create a dummy UdpSendContext
        var sendContext = new UdpSendContext(
            "session1",
            new Memory<byte>(new byte[] { 1, 2, 3 }),
            null,
            new IPEndPoint(IPAddress.Loopback, 12345),
            42
        );

        // Track should add to unacked
        tracker.Track(sendContext);

        // ResendUnacked should call onSend

        await Task.Delay(300); // Allow some time for the tracker to process
        tracker.ResendUnacked(DateTime.UtcNow);
        Assert.True(onSendCalled);

        // Ack should remove from unacked
        tracker.Acknowledge(42);
        // No exception means success

        await Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateReceived_Calls_OnReceived_For_InOrder_And_OutOfOrder()
    {
        var tracker = new UdpTracker();
        List<byte> receivedPackets = new();

        tracker.Run(
            ctx => Task.CompletedTask,
            ctx => { receivedPackets.Add(ctx.Payload.Span[0]); return Task.CompletedTask; }
        );

        var remote = new IPEndPoint(IPAddress.Loopback, 12345);

        // In-order packet
        var ctx1 = new UdpReceivedContext(remote, "session1", 0, 0, PacketType.Ping, new byte[] { 1 }, null);
        tracker.UpdateReceived(ctx1);

        // Out-of-order packet (sequence 2 arrives before 1)
        var ctx3 = new UdpReceivedContext(remote, "session1", 2, 0, PacketType.Ping, new byte[] { 3 }, null);
        tracker.UpdateReceived(ctx3);

        var ctx2 = new UdpReceivedContext(remote, "session1", 1, 0, PacketType.Ping, new byte[] { 2 }, null);
        tracker.UpdateReceived(ctx2);

        // Wait a bit to ensure all packets are processed
        await Task.Delay(100);
        Assert.Equal(new byte[] { 1, 2, 3 }, receivedPackets.ToArray());

        await Task.CompletedTask;
    }
}