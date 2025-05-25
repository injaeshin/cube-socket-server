using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Cube.Network.Buffer;

namespace Cube.Network.Transport;

public class TcpTransport(ILoggerFactory loggerFactory, TransportReturnEvent returnEvent) : ITransport
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TcpTransport>();
    private readonly TransportReturnEvent _returnEvent = returnEvent;
    private readonly PacketBuffer _receiveBuffer = new PacketBuffer();

    private Socket _socket = null!;
    private SocketAsyncEventArgs _receiveArgs = null!;
    private ITransportNotify _notify = null!;

    private bool _isClosed;
    private readonly object _lockRelease = new();

    public void BindNotify(ITransportNotify notify) => _notify = notify;

    public void BindSocket(Socket socket, SocketAsyncEventArgs receiveArgs)
    {
        _socket = socket;
        _receiveArgs = receiveArgs;
        _receiveArgs.UserToken = this;
        _receiveArgs.Completed += OnReceiveCompleted;
        ConfigureKeepAlive(_socket);
        ConfigureNoDelay(_socket);
    }

    public Socket GetSocket() => _socket;

    public void Run()
    {
        _isClosed = false;

        DoReceive();
        _notify.OnNotifyConnected();
    }

    public void Close(bool isGraceful = false)
    {
        CloseConnection(isGraceful);
        ReleaseResources();
    }

    private void CloseConnection(bool isGraceful)
    {
        if (_socket == null || _isClosed) return;

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
            _notify.OnNotifyDisconnected(isGraceful);
        }
    }

    private bool ReleaseResources()
    {
        lock (_lockRelease)
        {
            if (_isClosed) return false;
            _isClosed = true;
        }

        try
        {
            ReturnReceiveArgs();
            ReturnTransport();
            _receiveBuffer.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing resources - {Message}", ex.Message);
        }

        return true;
    }

    private void ReturnReceiveArgs()
    {
        if (_receiveArgs != null)
        {
            _receiveArgs.Completed -= OnReceiveCompleted;
            _receiveArgs.UserToken = null;
            _receiveArgs.AcceptSocket = null;
            _returnEvent.OnReleaseReceiveArgs(_receiveArgs);
            _receiveArgs = null!;
        }
    }

    private void ReturnTransport()
    {
        if (_socket != null)
        {
            _returnEvent.OnReleaseTransport(this);
            _socket = null!;
        }
    }

    public bool IsConnectionAlive()
    {
        if (_socket == null || _isClosed) return false;

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
            if (!_isClosed)
            {
                return;
            }
        }

        try
        {
            if (_receiveArgs.Buffer == null)
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
        return _socket?.Connected == true && _receiveArgs != null && !_isClosed;
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (!HandleReceiveResult(e))
        {
            return;
        }

        _ = HandleReceiveAsync(e);
    }

    private async Task ProcessReceivedData(SocketAsyncEventArgs e)
    {
        var data = e.MemoryBuffer[..e.BytesTransferred];
        _receiveBuffer.TryAppend(data);
        while (_receiveBuffer.TryGetValidatePacket(out var payload, out var rentedBuffer))
        {
            await _notify.OnNotifyReceived(payload, rentedBuffer);
        }
    }

    private bool HandleReceiveResult(SocketAsyncEventArgs e)
    {
        if (e.LastOperation != SocketAsyncOperation.Receive)
        {
            Close();
            return false;
        }

        if (e.SocketError != SocketError.Success)
        {
            Close();
            return false;
        }

        if (e.BytesTransferred == 0)
        {
            Close(isGraceful: true);
            return false;
        }

        return true;
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
            _notify.OnNotifyError(ex);
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
        if (_isClosed) return;
        Close();
    }
}