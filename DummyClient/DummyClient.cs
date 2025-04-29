using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using Common.Network;
using Common.Network.Packet;
using Common.Network.Message;
using Common.Network.Buffer;
using System.Threading.Channels;
using Common.Network.Session;

namespace __DummyClient;

public partial class DummyClient
{
    private readonly ILogger<DummyClient> _logger;
    private readonly string _name;
    private readonly string _host;
    private readonly int _port;

    private readonly byte[] _buffer = new byte[NetConsts.BUFFER_SIZE];
    private readonly SocketAsyncEventArgs _receiveArgs = new();
    
    private readonly IDummySession _session;
    private bool _running = false;
    private Task? _chatTask;

    public DummyClient(ILoggerFactory loggerFactory, string name, string host, int port)
    {
        _name = name;
        _host = host;
        _port = port;

        _logger = loggerFactory.CreateLogger<DummyClient>();
        _session = new DummySession(loggerFactory, OnReceivedPacketAsync);
    }

    public async Task RunAsync()
    {
        try
        {
            if (!await ConnectToServerAsync())
                return;

            _running = true;
            await SendLoginRequestAsync(_name);

            _chatTask = StartChattingAsync();
            await _chatTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] RunAsync error", _name);
        }
    }

    public async Task StopAsync()
    {
        _running = false;
        try
        {
            if (_chatTask != null)
                await _chatTask;

            _session.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] StopAsync error", _name);
        }
    }

    private async Task SendLoginRequestAsync(string username)
    {
        try
        {
            using var writer = new PacketWriter();
            writer.WriteType(MessageType.Login).WriteString(username);
            var packet = writer.ToPacket();
            await SendPacketAsync(packet);
            _logger.LogInformation("[{name}] Login request sent", _name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{name}] Login error", _name);
        }
    }

    private async Task StartChattingAsync()
    {
        var rand = new Random();
        while (_running)
        {
            await Task.Delay(rand.Next(1000, 3000)); // 1~3초 랜덤
            var msg = $"Hello from {_name} at {DateTime.Now:HH:mm:ss}";
            await SendChatMessageAsync(msg);
        }
    }
}