using System.Collections.Concurrent;
using Cube.Core.Settings;
using Cube.Packet;
using Cube.Packet.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Sessions;

public interface ISessionHeartbeat
{
    void RegisterSession(ISession session);
    void UnregisterSession(string sessionId);
    void UpdateSessionActivity(string sessionId);
}

public interface IHeartbeat : ISessionHeartbeat
{
    bool IsSessionActive(string sessionId);
    void Close();
}


public class SessionHeartbeat(ILogger<SessionHeartbeat> logger, HeartbeatConfig heartbeatConfig) : IHostedService, IHeartbeat
{
    private readonly ILogger<SessionHeartbeat> _logger = logger;
    private readonly ConcurrentDictionary<string, HeartbeatState> _sessions = new();
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(heartbeatConfig.Interval);
    private readonly TimeSpan _pingTimeout = TimeSpan.FromSeconds(heartbeatConfig.PingTimeout);
    private Task? _workTask;
    private CancellationTokenSource _cts = null!;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workTask = WorkAsync(_cts.Token);
        _logger.LogDebug("Session heartbeat monitor started - Hashcode: {hashcode}", this.GetHashCode());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_workTask != null)
        {
            _cts.Cancel();
            try
            {
                await _workTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping session heartbeat monitor");
            }
        }

        _cts.Dispose();
        _logger.LogDebug("Session heartbeat monitor stopped - Hashcode: {hashcode}", this.GetHashCode());
    }

    public bool IsSessionActive(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            return s.IsActive;
        }
        _logger.LogWarning("Session {sessionId} not found", sessionId);
        return false;
    }

    public void Close()
    {
        if (!_sessions.IsEmpty)
        {
            _sessions.Clear();
        }
    }

    public void RegisterSession(ISession session)
    {
        _sessions[session.SessionId] = new HeartbeatState(session);
        _logger.LogInformation("Session {sessionId} registered - Hashcode: {hashcode}", session.SessionId, this.GetHashCode());
    }

    public void UnregisterSession(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogError("Session {sessionId} not found", sessionId);
        }

        _logger.LogInformation("Session {sessionId} unregistered - Hashcode: {hashcode}", sessionId, this.GetHashCode());
    }

    public void UpdateSessionActivity(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            s.LastActivity = DateTime.UtcNow;

            if (s.IsPingSent)
            {
                s.SetActive();
            }
        }
    }

    private async Task CheckSessionHeartbeat()
    {
        try
        {
            var now = DateTime.UtcNow;
            var sessionToProcess = _sessions.ToArray();
            foreach (var (_, ss) in sessionToProcess)
            {
                switch (ss.StateType)
                {
                    case HeartbeatStateType.Active:
                        if (now - ss.LastActivity > _heartbeatInterval)
                        {
                            await SendPingAsync(ss);
                        }
                        break;
                    case HeartbeatStateType.PingSent:
                        if (now - ss.LastPingTime > _pingTimeout)
                        {
                            CloseTimeoutSession(ss);
                        }
                        break;
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking session heartbeat");
        }

        await Task.CompletedTask;
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_heartbeatInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckSessionHeartbeat();
            }
        }
        catch (OperationCanceledException)
        {
            // Task was cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring sessions");
        }
    }

    private async Task SendPingAsync(HeartbeatState hs)
    {
        try
        {
            if (!hs.Session.IsConnected)
            {
                UnregisterSession(hs.Session.SessionId);
                return;
            }

            var (data, rentedBuffer) = new PacketWriter(PacketType.Ping).ToTcpPacket();
            await hs.Session.SendAsync(data, rentedBuffer);

            hs.LastPingTime = DateTime.UtcNow;
            hs.SetPingSent();
            _logger.LogDebug("Sent ping to session {sessionId}", hs.Session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ping to session {sessionId}", hs.SessionId);
            CloseTimeoutSession(hs);
        }
    }

    private void CloseTimeoutSession(HeartbeatState hs)
    {
        try
        {
            _logger.LogDebug("Closing timeout session {sessionId}", hs.SessionId);
            hs.Session.Kick(ErrorType.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing timeout session {sessionId}", hs.SessionId);
        }
    }

    private class HeartbeatState(ISession session)
    {
        public ISession Session { get; init; } = session;
        public string SessionId => Session.SessionId;
        public HeartbeatStateType StateType { get; private set; }
        public void SetActive() => StateType = HeartbeatStateType.Active;
        public bool IsActive => StateType == HeartbeatStateType.Active;
        public void SetPingSent() => StateType = HeartbeatStateType.PingSent;
        public bool IsPingSent => StateType == HeartbeatStateType.PingSent;
        public void SetPingReceived() => StateType = HeartbeatStateType.PingReceived;
        public bool IsPingReceived => StateType == HeartbeatStateType.PingReceived;
        public void SetTimeout() => StateType = HeartbeatStateType.Timeout;
        public bool IsTimeout => StateType == HeartbeatStateType.Timeout;

        public DateTime LastActivity { get; set; }
        public DateTime LastPingTime { get; set; }
    }
}