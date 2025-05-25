//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Logging;
//using Common.Network.Pool;

//namespace DummyClient;

//public class Client
//{
//    private readonly ILogger<Client> _logger;
//    private readonly ILoggerFactory _loggerFactory;
//    private ClientSession _clientSession;
//    private readonly string _username;
//    public Client(ILoggerFactory loggerFactory, string username)
//    {
//        _loggerFactory = loggerFactory;
//        _logger = loggerFactory.CreateLogger<Client>();
//        _clientSession = new ClientSession(_loggerFactory);

//        _username = username;
//    }

//    public async Task RunAsync(string host, int port, CancellationToken token)
//    {
//        try
//        {
//            await _clientSession.ConnectAsync(host, port);
//            _ = _clientSession.RunAsync(token);

//            await _clientSession.Sender.SendLoginAsync(_username);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "[{user}] Error", _username);
//        }
//    }

//    public void Close()
//    {
//        _clientSession.Stop();
//    }
//}