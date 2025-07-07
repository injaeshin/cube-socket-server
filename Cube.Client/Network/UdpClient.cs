using System.Net;
using Cube.Core.Network;
using Cube.Packet;
using Cube.Packet.Builder;

namespace Cube.Client.Network;

public class UdpClient : IDisposable
{
    private string _ip;
    private int _port;

    private readonly UdpSocket _socket;
    private readonly UdpTracker _tracker;
    private EndPoint _remoteEndpoint = null!;

    private bool _disposed = false;
    private bool _isConnected = false;
    private string _sessionToken = string.Empty;

    public event Action<string>? OnStatusChanged;
    public event Action<PacketType, ReadOnlyMemory<byte>>? OnPacketReceived;

    public UdpClient(string serverAddress, int serverPort)
    {
        _ip = serverAddress;
        _port = serverPort;
        _socket = new UdpSocket();
        _socket.OnStatusChanged += status => OnStatusChanged?.Invoke(status);
        _socket.OnDataReceived += OnSocketDataReceived;

        _tracker = new UdpTracker();
        _tracker.Run(
            async ctx => await Task.Run(() => _socket.Send(ctx.Data)),
            async ctx => await Task.Run(() =>
            {
                OnPacketReceived?.Invoke(ctx.PacketType, ctx.Payload);
            })
        );

        _remoteEndpoint = new IPEndPoint(IPAddress.Parse(_ip), _port);
    }

    private void OnSocketDataReceived(UdpReceivedContext ctx)
    {
        if (!_isConnected) throw new InvalidOperationException("연결되지 않은 상태입니다.");
        if (_tracker == null) throw new InvalidOperationException("UDP 시퀀스 추적기가 초기화되지 않았습니다.");

        if (ctx.SessionId != _sessionToken)
        {
            throw new InvalidOperationException("세션 토큰이 일치하지 않습니다.");
        }

        PreProcess(ctx);
        _tracker.UpdateReceived(ctx);
    }

    private void PreProcess(UdpReceivedContext ctx)
    {
        if (ctx.PacketType == PacketType.Ack)
        {
            Console.WriteLine($"Ack {ctx.Ack}");
            _tracker.Acknowledge(ctx.Ack);
            return;
        }

        // remote endpoint 에 ack 보내주기
        SendAck(ctx.Sequence);
    }

    private void SendAck(ushort ack)
    {
        var (data, rentedBuffer) = new PacketWriter(PacketType.Ack).ToUdpPacket();
        data.SetUdpAckHeader(_sessionToken, ack);
        _socket.Send(data);
        BufferArrayPool.Return(rentedBuffer);
    }

    public void Connect(string sessionToken)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("이미 연결된 상태입니다.");
        }

        if (string.IsNullOrEmpty(sessionToken))
        {
            throw new ArgumentException("세션 토큰이 없습니다.", nameof(sessionToken));
        }

        try
        {
            _sessionToken = sessionToken;
            _socket.Connect(_ip, _port);
            _isConnected = true;

            // KnockKnock 패킷 전송
            var (data, rentedBuffer) = new PacketWriter(PacketType.KnockKnock).ToUdpPacket();
            Send(data, rentedBuffer);
        }
        catch (Exception ex)
        {
            throw new Exception($"UDP 연결 실패: {ex.Message}");
        }
    }

    public void Send(Memory<byte> data, byte[]? rentedBuffer)
    {
        if (!_isConnected || _tracker == null)
        {
            throw new InvalidOperationException("연결되지 않은 상태입니다.");
        }

        if (string.IsNullOrEmpty(_sessionToken))
        {
            throw new InvalidOperationException("세션 토큰이 없습니다.");
        }

        try
        {
            var seq = _tracker.NextSequence();
            data.SetUdpHeader(_sessionToken, seq);
            var sendContext = new UdpSendContext(_sessionToken, data, rentedBuffer, _remoteEndpoint, seq);
            _tracker.Track(sendContext);
            _socket.Send(data);
        }
        catch (Exception ex)
        {
            throw new Exception($"UDP 메시지 전송 실패: {ex.Message}");
        }
    }

    public void Close()
    {
        if (_isConnected)
        {
            _socket.Close();
            _isConnected = false;
            _sessionToken = string.Empty;
            _tracker?.Clear();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            Close();
            _socket.Dispose();
        }
    }
}