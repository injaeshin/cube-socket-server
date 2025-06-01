using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Core.Pool;
using Cube.Packet;

namespace Cube.Core.Network;

public interface ITcpConnection : IDisposable
{
    void Bind(Socket socket, PacketBuffer receiveBuffer, SocketAsyncEventArgs receiveArgs);
    void BindNotify(INotifySession notify);

    void Run();
    void Close(bool isGraceful = false);

    bool IsConnectionAlive();
    Socket GetSocket();
}

public class TcpConnection : ITcpConnection
{
    private readonly ILogger _logger;
    private readonly ITcpConnectionPool _tcpConnectionPool;

    private Socket _socket = null!;
    private SocketAsyncEventArgs _receiveArgs = null!;
    private PacketBuffer _receiveBuffer = null!;
    private INotifySession _notify = null!;

    private bool _closed;
    private readonly object _lockRelease = new();

    public TcpConnection(ILoggerFactory loggerFactory, ITcpConnectionPool tcpConnectionPool)
    {
        _logger = loggerFactory.CreateLogger<TcpConnection>();
        _tcpConnectionPool = tcpConnectionPool;
    }

    public void BindNotify(INotifySession notify)
    {
        _notify = notify;
    }

    public void Bind(Socket socket, PacketBuffer receiveBuffer, SocketAsyncEventArgs receiveArgs)
    {
        _socket = socket;
        _receiveBuffer = receiveBuffer;
        _receiveArgs = receiveArgs;
        _receiveArgs.UserToken = this;
        _receiveArgs.Completed += OnReceiveCompleted;

        ConfigureKeepAlive(_socket);
        ConfigureNoDelay(_socket);
    }

    public Socket GetSocket() => _socket;

    public void Run()
    {
        _closed = false;

        DoReceive();
        _notify.OnNotifyConnected(TransportType.Tcp);
    }

    public void Close(bool isGraceful = false)
    {
        if (_socket == null || _closed) return;

        lock (_lockRelease)
        {
            if (_closed) return;
            _closed = true;
        }

        try
        {            
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloseConnection] Error: {ex.Message}");
        }
        finally
        {
            ReleaseResources();
            _notify.OnNotifyDisconnected(TransportType.Tcp, isGraceful);
        }
    }

    private void ReleaseResources()
    {
        try
        {
            if (_receiveArgs != null)
            {
                _receiveArgs.Completed -= OnReceiveCompleted;
                _receiveArgs.UserToken = null;
                _receiveArgs.AcceptSocket = null;
                _tcpConnectionPool.ReturnSocketAsyncEventArgs(_receiveArgs);
                _receiveArgs = null!;
            }

            if (_receiveBuffer != null)
            {
                _tcpConnectionPool.ReturnPacketBuffer(_receiveBuffer);
                _receiveBuffer = null!;
            }

            if (_socket != null)
            {
                _tcpConnectionPool.Return(this);
                _socket = null!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing resources - {Message}", ex.Message);
        }
    }

    public bool IsConnectionAlive()
    {
        if (_socket == null || _closed) return false;

        try
        {
            bool isDisconnected = _socket.Poll(1, SelectMode.SelectRead) && _socket.Available == 0;
            if (isDisconnected)
            {
                _logger.LogDebug("[Connection Broken] detected via Poll");
                return false;
            }

            return !isDisconnected;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug("[Connection Exception] {Message}", ex.Message);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void DoReceive()
    {
        if (!IsReceiveReady())
        {
            if (!_closed)
            {
                return;
            }
        }

        try
        {
            if (_receiveArgs.MemoryBuffer.IsEmpty)
            {
                _logger.LogError("[DoReceive] Receive buffer is null");
                Close();
                return;
            }

            bool pending = _socket.ReceiveAsync(_receiveArgs);
            if (!pending)
            {
                OnReceiveCompleted(null, _receiveArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DoReceive] ReceiveAsync 호출 중 오류 발생");
            Close();
        }
    }

    private bool IsReceiveReady()
    {
        return _socket?.Connected == true && _receiveArgs != null && !_closed;
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.LastOperation != SocketAsyncOperation.Receive)
        {
            Close();
            return;
        }

        if (e.SocketError != SocketError.Success)
        {
            Close();
            return;
        }

        if (e.BytesTransferred == 0)
        {
            Close(isGraceful: true);
            return;
        }

        _ = HandleReceiveAsync(e);
    }

    private async Task ProcessReceivedData(SocketAsyncEventArgs e)
    {
        if (!_receiveBuffer.TryAppend(e.MemoryBuffer, e.BytesTransferred))
        {
            _logger.LogError("[ProcessReceivedData] Failed to append received data");
            Close();
            return;
        }

        while (_receiveBuffer.TryGetValidatePacket(out var packetType, out var payload, out var rentedBuffer))
        {
            var ctx = new ReceivedContext(_notify.SessionId, packetType, payload, rentedBuffer);
            await _notify.OnNotifyReceived(ctx);
        }
    }

    private async Task HandleReceiveAsync(SocketAsyncEventArgs e)
    {
        try
        {
            await ProcessReceivedData(e);
            DoReceive();
        }
        catch (Exception ex)
        {
            _notify.OnNotifyError(TransportType.Tcp, ex);
            Close();
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
        if (_closed) return;
        Close();
    }
}