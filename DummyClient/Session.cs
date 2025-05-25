//using System.Net.Sockets;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using Common.Network;
//using Common.Network.Packet;
//using System.Threading;

//namespace DummyClient;

//public class ClientSession
//{
//    private TcpClient? _client;
//    private NetworkStream? _stream;
//    private readonly ILogger _logger;
//    private readonly MessageHandler _messageHandler;
//    private readonly MessageSender _messageSender;
//    private CancellationTokenSource? _cts;

//    public ClientSession(ILoggerFactory loggerFactory)
//    {
//        _messageHandler = new MessageHandler(loggerFactory.CreateLogger<MessageHandler>());
//        _messageSender = new MessageSender(this, loggerFactory.CreateLogger<MessageSender>());
//        _logger = loggerFactory.CreateLogger<ClientSession>();
//    }

//    public async Task<bool> ConnectAsync(string host, int port)
//    {
//        try
//        {
//            _client = new TcpClient();
//            await _client.ConnectAsync(host, port);
//            _stream = _client.GetStream();
//            _logger.LogInformation("세션 연결됨");
//            return true;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "연결 실패");
//            return false;
//        }
//    }

//    public bool IsConnectionAlive()
//    {
//        return _client?.Connected ?? false;
//    }

//    public void Close()
//    {
//        _stream?.Close();
//        _client?.Close();
//        _logger.LogInformation("세션 연결 해제");
//    }

//    public async Task SendAsync(ReadOnlyMemory<byte> data)
//    {
//        if (_stream != null)
//            await _stream.WriteAsync(data);
//    }

//    public async Task<int> ReceiveAsync(byte[] buffer)
//    {
//        if (_stream != null)
//        {
//            int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
//            if (bytesRead > 4) // 최소 4바이트: 2바이트 길이, 2바이트 타입
//            {
//                var type = (MessageType)BitConverter.ToUInt16(buffer, 2);
//                var payload = new ReadOnlyMemory<byte>(buffer, 4, bytesRead - 4);
//                await OnProcessReceivedAsync(type, payload);
//            }
//            return bytesRead;
//        }
//        return 0;
//    }

//    public async Task OnProcessReceivedAsync(MessageType type, ReadOnlyMemory<byte> payload)
//    {
//        await _messageHandler.HandlePacket(this, type, payload);
//    }

//    public MessageSender Sender => _messageSender;

//    public async Task RunAsync(CancellationToken token)
//    {
//        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
//        var buffer = new byte[4096];
//        try
//        {
//            while (IsConnectionAlive() && !_cts.Token.IsCancellationRequested)
//            {
//                int bytesRead = await ReceiveAsync(buffer);
//                if (bytesRead == 0)
//                    break;
//            }
//        }
//        catch (TaskCanceledException) { }
//        catch (OperationCanceledException) { }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "수신 루프 오류");
//        }
//    }

//    public void Stop()
//    {
//        _cts?.Cancel();
//        Close();
//    }
//}