using System.Net;
using System.Net.Sockets;
using Cube.Core.Network;
using Cube.Packet;

namespace Cube.Client.Network;

public class UdpSocket : IDisposable
{
    private readonly Socket _socket;
    private readonly SocketAsyncEventArgs _sendEventArgs;
    private readonly SocketAsyncEventArgs _receiveEventArgs;

    public event Action<string>? OnStatusChanged;
    public event Action<UdpReceivedContext>? OnDataReceived;

    private bool _disposed = false;
    private bool _isConnected = false;

    public UdpSocket()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        // 수신용 SocketAsyncEventArgs 설정
        _receiveEventArgs = new SocketAsyncEventArgs();
        _receiveEventArgs.SetBuffer(new byte[PacketConsts.BUFFER_SIZE]);
        _receiveEventArgs.Completed += OnReceiveCompleted;

        // 송신용 SocketAsyncEventArgs 설정
        _sendEventArgs = new SocketAsyncEventArgs();
        _sendEventArgs.Completed += OnSendCompleted;
    }

    public void Connect(string address, int port)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("이미 연결된 상태입니다.");
        }

        try
        {
            _sendEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _isConnected = true;
            StartReceive();
        }
        catch (Exception ex)
        {
            throw new Exception($"UDP 연결 실패: {ex.Message}");
        }
    }

    public void Send(Memory<byte> data)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("연결되지 않은 상태입니다.");
        }

        try
        {
            _sendEventArgs.SetBuffer(data);

            if (!_socket.SendToAsync(_sendEventArgs))
            {
                OnSendCompleted(this, _sendEventArgs);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"UDP 메시지 전송 실패: {ex.Message}");
        }
    }

    private void StartReceive()
    {
        if (!_isConnected) return;

        _receiveEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            if (!_socket.ReceiveFromAsync(_receiveEventArgs))
            {
                OnReceiveCompleted(this, _receiveEventArgs);
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"UDP 수신 시작 오류: {ex.Message}");
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            OnStatusChanged?.Invoke($"UDP 수신 오류: {e.SocketError}");
            return;
        }

        if (e.BytesTransferred > 0)
        {
            ReadOnlyMemory<byte> data = e.MemoryBuffer[..e.BytesTransferred];
            if (data.TryGetValidateUdpPacket(out var token, out var sequence, out var ack, out var packetType, out var payload, out var rentedBuffer))
            {
                OnStatusChanged?.Invoke($"UDP 패킷 수신: {packetType}, Seq: {sequence}, Ack: {ack}, Payload: {payload.ToHexDump()}");

                var context = new UdpReceivedContext(
                    _sendEventArgs.RemoteEndPoint!,
                    token,
                    sequence,
                    ack,
                    packetType,
                    payload,
                    rentedBuffer);

                OnDataReceived?.Invoke(context);
            }
        }

        if (_isConnected)
        {
            StartReceive();
        }
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            OnStatusChanged?.Invoke($"UDP 송신 오류: {e.SocketError}");
        }
    }

    public void Close()
    {
        if (_isConnected)
        {
            _socket.Close();
            _isConnected = false;
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
            _sendEventArgs.Dispose();
            _receiveEventArgs.Dispose();
        }
    }
}