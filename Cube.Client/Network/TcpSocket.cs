using Cube.Packet;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace Cube.Client.Network;

public class TcpSocket : IDisposable
{
    private readonly Socket _socket;
    private readonly PacketBuffer _receiveBuffer;
    private readonly SocketAsyncEventArgs _receiveEventArgs;
    private readonly SocketAsyncEventArgs _sendEventArgs;

    private bool _disposed;
    private bool _isConnected;
    private readonly object _lockRelease = new();

    public event Action<string>? OnStatusChanged;
    public event Action<PacketType, ReadOnlyMemory<byte>>? OnDataReceived;

    public TcpSocket()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        _receiveBuffer = new PacketBuffer();
        _receiveBuffer.Initialize(new Memory<byte>(new byte[2048]));

        _receiveEventArgs = new SocketAsyncEventArgs();
        _receiveEventArgs.SetBuffer(new Memory<byte>(new byte[2048]));
        _receiveEventArgs.Completed += OnReceiveCompleted;

        _sendEventArgs = new SocketAsyncEventArgs();
        _sendEventArgs.Completed += OnSendCompleted;

        ConfigureKeepAlive(_socket);
        ConfigureNoDelay(_socket);
    }

    public void Connect(string address, int port)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("이미 연결된 상태입니다.");
        }

        try
        {
            _socket.Connect(address, port);
            _isConnected = true;
            DoReceive();
            OnStatusChanged?.Invoke("TCP 연결 성공");
        }
        catch (Exception ex)
        {
            throw new Exception($"TCP 연결 실패: {ex.Message}");
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
            if (!_socket.SendAsync(_sendEventArgs))
            {
                OnSendCompleted(this, _sendEventArgs);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"TCP 메시지 전송 실패: {ex.Message}");
        }
    }

    private void DoReceive()
    {
        if (!_isConnected) return;

        try
        {
            if (!_socket.ReceiveAsync(_receiveEventArgs))
            {
                OnReceiveCompleted(this, _receiveEventArgs);
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"TCP 수신 시작 오류: {ex.Message}");
            Disconnect();
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            OnStatusChanged?.Invoke($"TCP 수신 오류: {e.SocketError}");
            Disconnect();
            return;
        }

        if (e.BytesTransferred == 0)
        {
            Disconnect(true);
            return;
        }

        HandleReceiveAsync(e);
    }

    private void HandleReceiveAsync(SocketAsyncEventArgs e)
    {
        try
        {
            if (!_receiveBuffer.TryAppend(e.MemoryBuffer[..e.BytesTransferred]))
            {
                OnStatusChanged?.Invoke("TCP 수신 버퍼 오버플로우");
                Disconnect();
                return;
            }

            while (_receiveBuffer.TryGetValidatePacket(out var packetType, out var payload, out var rentedBuffer))
            {
                OnDataReceived?.Invoke(packetType, payload);
            }

            DoReceive();
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"TCP 데이터 처리 오류: {ex.Message}");
            Disconnect();
        }
    }
    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            OnStatusChanged?.Invoke($"TCP 송신 오류: {e.SocketError}");
            Disconnect();
        }
    }

    public void Disconnect(bool isGraceful = false)
    {
        if (!_isConnected) return;

        lock (_lockRelease)
        {
            if (!_isConnected) return;
            _isConnected = false;
        }

        try
        {
            if (_socket.Connected)
            {
                if (isGraceful)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                _socket.Close();
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"TCP 연결 종료 오류: {ex.Message}");
        }
    }

    private static void ConfigureKeepAlive(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        int keepAliveTime = 15;
        int keepAliveInterval = 5;
        int keepAliveRetryCount = 3;

        if (OperatingSystem.IsWindows())
        {
            byte[] keepAliveValues = new byte[12];
            BitConverter.GetBytes(1).CopyTo(keepAliveValues, 0);
            BitConverter.GetBytes(keepAliveTime * 1000).CopyTo(keepAliveValues, 4);
            BitConverter.GetBytes(keepAliveInterval * 1000).CopyTo(keepAliveValues, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, keepAliveValues, null);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTime);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveInterval);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, keepAliveRetryCount);
        }
    }

    private static void ConfigureNoDelay(Socket socket)
    {
        if (socket.ProtocolType == ProtocolType.Tcp)
        {
            socket.NoDelay = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnect();
        _socket.Dispose();
        _receiveBuffer.Dispose();
        _sendEventArgs.Dispose();
        _receiveEventArgs.Dispose();
    }
}