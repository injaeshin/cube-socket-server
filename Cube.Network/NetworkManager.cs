using System.Net.Sockets;
using Microsoft.Extensions.Logging;

using Cube.Network.Acceptor;
using Cube.Network.Channel;
using Cube.Network.Context;
using Cube.Network.Pool;
using Cube.Network.Transport;
using System.Net;

namespace Cube.Network;

public interface ISessionCreator
{
    bool CreateAndRunSession(Socket socket, ITransport transport);
}

public interface INetworkManager
{
    void BindSessionCreator(ISessionCreator sessionCreator);
    void Run(TransportType transportType, int port);
    void Stop();
    Task OnSendEnqueueAsync(string sessionId, Memory<byte> data, byte[]? rentedBuffer, Socket socket);
}

public class NetworkManager : INetworkManager
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly SocketAsyncEventArgsPool _saeaPool;
    private readonly TransportPool _transportPool;
    private readonly PoolEvent _poolEvent;

    private TcpAcceptor _tcpAcceptor = null!;
    private TcpSender _tcpSender = null!;

    private UdpSender _udpSender = null!;
    private UdpReceiver _udpReceiver = null!;

    private ISessionCreator? _sessionCreator;


    private bool _closed = false;

    public NetworkManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<NetworkManager>();

        _saeaPool = new SocketAsyncEventArgsPool(loggerFactory, NetConsts.MAX_CONNECTIONS);
        _poolEvent = new PoolEvent
        {
            OnRentEventArgs = _saeaPool.Rent,
            OnReleaseEventArgs = _saeaPool.Return,
            OnGetCount = () => _saeaPool.Count
        };

        _transportPool = new TransportPool(loggerFactory, _poolEvent);
    }

    public void BindSessionCreator(ISessionCreator sessionCreator) => _sessionCreator = sessionCreator;

    public void Run(TransportType transportType, int port)
    {
        if (_closed) throw new InvalidOperationException("NetworkManager is closed");
        if (_sessionCreator == null) throw new InvalidOperationException("SessionCreator is not bound");
        if (port <= 0) throw new InvalidOperationException("Port is not set");

        switch (transportType)
        {
            case TransportType.Tcp:
                _tcpSender = new TcpSender(_loggerFactory, _poolEvent, OnSendCompleted);
                _tcpAcceptor = new TcpAcceptor(_loggerFactory, _poolEvent, OnClientConnected);

                _ = _tcpAcceptor.Run(port);
                break;
            case TransportType.Udp:

                _udpSender = new UdpSender(_loggerFactory, _poolEvent, new TransportUdpSendEvent { OnUdpPresetSend = OnUdpPresetSend });
                _udpReceiver = new UdpReceiver(_loggerFactory, _poolEvent, new TransportUdpReceiveEvent { OnUdpGreetingReceived = OnUdpGreetingReceived, OnUdpDatagramReceived = OnUdpDatagramReceived });

                _ = _udpSender.Run();
                _ = _udpReceiver.Run(port);
                break;
        }
    }

    public void Stop()
    {
        Close();
    }

    private async Task OnClientConnected(Socket socket)
    {
        var transport = _transportPool.Rent(socket);
        if (_sessionCreator == null || !_sessionCreator.CreateAndRunSession(socket, transport))
        {
            _transportPool.Return(transport);
            _logger.LogError("Session 생성 실패");
        }

        await Task.CompletedTask;
    }

    public async Task OnSendEnqueueAsync(string sessionId, Memory<byte> data, byte[]? rentedBuffer, Socket socket)
    {
        var context = new TcpSendContext(sessionId, data, rentedBuffer, socket);
        await _tcpSender.SendAsync(context);
    }

    private async Task OnSendCompleted(TcpSendContext context)
    {
        await Task.CompletedTask;
    }

    public async Task OnSendEnqueueAsync(string sessionId, Memory<byte> data, byte[]? rentedBuffer, EndPoint remoteEndPoint, ushort sequence, ushort ack)
    {
        var context = new UdpSendContext(sessionId, data, rentedBuffer, remoteEndPoint, sequence, ack);
        await _udpSender.SendAsync(context);
    }

    private bool OnUdpPresetSend(string sessionId, ushort seq, ReadOnlyMemory<byte> payload)
    {
        //     if (!_sessionManager.TryGetUdpSession(sessionId, out var udpSession))
        //     {
        //         return;
        //     }

        //     udpSession!.Track(seq, payload);

        return true;
    }

    // private async Task UdpSendToAsync(EndPoint ep, ushort ack, ReadOnlyMemory<byte> payload)
    // {
    //     if (!_sessionManager.TryGetSessionId(ep, out var sessionId))
    //     {
    //         return;
    //     }

    //     if (!_sessionManager.TryGetUdpSession(sessionId, out var udpSession))
    //     {
    //         _logger.LogWarning("UDP 세션 획득 실패: {EndPoint}, sessionId: {SessionId}", ep, sessionId);
    //         return;
    //     }

    //     if (!_sessionManager.TryGetSession(sessionId, out var session))
    //     {
    //         _logger.LogWarning("세션 획득 실패: {EndPoint}, sessionId: {SessionId}", ep, sessionId);
    //         return;
    //     }

    //     var nextSeq = udpSession!.NextSequence;
    //     udpSession.Track(nextSeq, payload);
    //     //_udpSender.SendAsync(ep, nextSeq, payload);

    //     await Task.CompletedTask;
    // }

    private void OnUdpGreetingReceived(EndPoint ep, string sessionId)
    {
        _logger.LogInformation("UDP 세션 획득: {EndPoint}, sessionId: {SessionId}", ep, sessionId);
        // if (!_sessionCreator.TryGetUdpSession(sessionId, out var udpSession))
        // {
        //     _sessionManager.CreateUdpSession(sessionId, ep, UdpSendToAsync);
        //     _sessionManager.SetEndPointToSessionId(ep, sessionId);
        //     _logger.LogWarning("UDP 세션 획득 실패: {EndPoint}, sessionId: {SessionId}", ep, sessionId);
        //     return;
        // }

        // if (!udpSession!.RemoteEndPoint.Equals(ep))
        // {
        //     _sessionManager.RemoveEndPointToSessionId(udpSession.RemoteEndPoint);
        //     _sessionManager.SetEndPointToSessionId(ep, sessionId);

        //     udpSession.RemoteEndPoint = ep;
        // }
    }

    private void OnUdpDatagramReceived(EndPoint ep, ReadOnlyMemory<byte> raw)
    {
        _logger.LogInformation("UDP 데이터 수신: {EndPoint}, 데이터 크기: {DataSize}", ep, raw.Length);
        //     if (!_sessionManager.TryGetSessionId(ep, out var sessionId))
        //     {
        //         return;
        //     }

        //     if (!PacketHelper.ParseRudpHeader(raw, out var sid, out var seq, out var ack, out var data))
        //     {
        //         _logger.LogWarning("UDP 데이터 수신: 잘못된 헤더 - 세션 ID: {SessionId}, 시퀀스: {Sequence}, 확인 번호: {Ack}", sessionId, seq, ack);
        //         return;
        //     }

        //     if (sessionId != sid)
        //     {
        //         _logger.LogWarning("UDP 데이터 수신: 세션 ID 불일치 - 예상: {Expected}, 실제: {Actual}", sessionId, sid);
        //         _sessionManager.RemoveEndPointToSessionId(ep);
        //         return;
        //     }

        //     if (!_sessionManager.TryGetUdpSession(sessionId, out var udpSession))
        //     {
        //         _logger.LogWarning("UDP 데이터 수신: 세션 상태 없음 - 세션 ID: {SessionId}", sessionId);
        //         _sessionManager.RemoveEndPointToSessionId(ep);
        //         return;
        //     }

        //     if (udpSession!.RemoteEndPoint.Equals(ep))
        //     {
        //         _logger.LogWarning("UDP 데이터 수신: 원격 엔드포인트 불일치 - 예상: {Expected}, 실제: {Actual}", udpSession.RemoteEndPoint, ep);
        //         _sessionManager.RemoveUdpSession(sessionId);
        //         _sessionManager.RemoveEndPointToSessionId(ep);
        //         return;
        //     }

        //     if (!_sessionManager.TryGetSession(sessionId, out var session))
        //     {
        //         _logger.LogWarning("UDP 데이터 수신: 세션 없음 - 세션 ID: {SessionId}", sessionId);
        //         return;
        //     }

        //     // ack 는 클라이언트가 바로 전에 받은 서버의 시퀀스 번호 [ ack 번호 잘 받았음이라는 통보 ]
        //     // 일반적인 TCP 처럼 클라이언트가 바로 전에 받은 서버의 시퀀스 번호 + 1 이라고 생각하면 안됨

        //     // 클라이언트가 잘 받았다면 보낸 패킷을 제거
        //     udpSession.Acknowledge(ack);

        //     // 받은 패킷은 버퍼에 저장함
        //     udpSession.UpdateReceived(ack, data);

        //     if (udpSession.TryReadPacket(out var packetType, out var payload, out var rentedBuffer))
        //     {
        //         _logger.LogInformation("UDP 데이터 수신: 패킷 타입: {PacketType}, 패킷 크기: {PacketSize}", packetType, payload.Length);
        //         _ = session?.OnNotifyReceived(packetType, payload, rentedBuffer);
        //     }
    }

    public void Close()
    {
        if (_closed) throw new InvalidOperationException("NetworkManager is closed");
        _closed = true;

        _tcpAcceptor?.Dispose();
        _udpSender?.Dispose();
        _tcpSender?.Stop();
        _udpReceiver?.Stop();

        _transportPool?.Close();
        _saeaPool?.Close();
    }
}

