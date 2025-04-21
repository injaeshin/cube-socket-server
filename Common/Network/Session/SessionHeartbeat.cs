using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Common.Network.Packet;

namespace Common.Network.Session;

public class SessionHeartbeat(ILogger<SessionHeartbeat> logger) : IHostedService
{
    private readonly ILogger<SessionHeartbeat> _logger = logger;
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _pingTimeout = TimeSpan.FromSeconds(5);
    private Task? _workTask;
    private CancellationTokenSource _cts = null!;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workTask = WorkAsync(_cts.Token);
        _logger.LogInformation("Session heartbeat monitor started");
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
        _logger.LogInformation("Session heartbeat monitor stopped");
    }

    public void RegisterSession(string sessionId, ISession session)
    {
        _sessions[sessionId] = new SessionInfo
        {
            Session = session,
            LastActivity = DateTime.UtcNow,
            LastPingTime = DateTime.UtcNow,
            State = SessionState.Active,
        };

        _logger.LogDebug("Session {sessionId} registered", sessionId);
    }

    public void UnregisterSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogDebug("Session {sessionId} unregistered", sessionId);
        }
    }

    public void UpdateSessionActivity(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var info))
        {
            info.LastActivity = DateTime.UtcNow;

            if (info.State == SessionState.PingSent)
            {
                info.State = SessionState.Active;
            }
        }
    }

    private async Task CheckSessionHeartbeat()
    {
        try
        {
            var now = DateTime.UtcNow;
            var sessionToProcess = _sessions.ToArray();

            foreach (var (session, info) in sessionToProcess)
            {
                if (!info.Session.IsConnectionAlive())
                {
                    UnregisterSession(session);
                    continue;
                }

                switch (info.State)
                {
                    case SessionState.Active:
                        if (now - info.LastActivity > _heartbeatInterval)
                        {
                            await SendPingAsync(info);
                        }
                        break;
                    case SessionState.PingSent:
                        if (now - info.LastPingTime > _pingTimeout)
                        {
                            CloseTimeoutSession(session, info);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking session heartbeat");
        }
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

    private async Task SendPingAsync(SessionInfo info)
    {
        try
        {
            await info.Session.SendAsync(PacketType.Ping, Array.Empty<byte>());

            info.LastPingTime = DateTime.UtcNow;
            info.State = SessionState.PingSent;
            _logger.LogDebug("Sent ping to session {sessionId}", info.Session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ping to session {sessionId}", info.Session.SessionId);
            CloseTimeoutSession(info.Session.SessionId, info);
        }
    }

    private void CloseTimeoutSession(string sessionId, SessionInfo info)
    {
        try
        {
            _logger.LogDebug("Closing timeout session {sessionId}", sessionId);
            info.Session.Close(DisconnectReason.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing timeout session {sessionId}", sessionId);
        }
        finally
        {
            UnregisterSession(sessionId);
        }
    }

    private class SessionInfo
    {
        public ISession Session { get; init; } = null!;
        public DateTime LastActivity { get; set; }
        public DateTime LastPingTime { get; set; }
        public SessionState State { get; set; }
    }

    private enum SessionState
    {
        Active,
        PingSent,
    }
}