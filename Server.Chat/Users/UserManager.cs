using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

using Common.Network.Session;

namespace Server.Chat.Users;

public interface IUserManager
{
    bool InsertUser(string username, ISession session);
    bool DeleteUser(string sessionId);
    IUser? GetUserBySession(string sessionId);
    IUser? GetUser(string userName);
    IEnumerable<IUser> GetAllUsers();
    bool IsAuthenticated(string sessionId);
    void End();
}

public class UserManager : IUserManager
{
    private readonly ConcurrentDictionary<string, IUser> _users = new();
    private readonly ConcurrentDictionary<string, string> _sessionToUserMap = new();
    private readonly ILogger _logger;

    public UserManager(ILogger<UserManager> logger)
    {
        _logger = logger;
    }

    public bool InsertUser(string username, ISession session)
    {
        // 이미 로그인한 사용자인지 확인
        if (_users.ContainsKey(username))
        {
            _logger.LogWarning("Already exists username: {Username}", username);
            return false;
        }

        // 세션에 이미 사용자가 할당되어 있는지 확인
        if (_sessionToUserMap.TryGetValue(session.SessionId, out var existingUser))
        {
            _logger.LogWarning("Already authenticated session: {SessionId}, user: {ExistingUser}", session.SessionId, existingUser);
            return false;
        }

        // 사용자 추가
        var user = new User(username, session);
        _users.TryAdd(username, user);
        _sessionToUserMap.TryAdd(session.SessionId, username);

        _logger.LogInformation("Added user: {Username}, {SessionId}", username, session.SessionId);
        _logger.LogDebug("Current users: {Users}", _users.Count);
        return true;
    }

    public bool DeleteUser(string sessionId)
    {
        if (!_sessionToUserMap.TryRemove(sessionId, out var username))
        {
            _logger.LogWarning("Not found session: {SessionId}", sessionId);
            return false;
        }

        if (!_users.TryRemove(username, out var user))
        {
            _logger.LogWarning("Not found user: {Username}", username);
            return false;
        }

        _logger.LogDebug("Removed user: {Username}, {SessionId}", username, sessionId);
        _logger.LogDebug("Current users: {Users}", _users.Count);
        return true;
    }

    public IUser? GetUserBySession(string sessionId)
    {
        if (_sessionToUserMap.TryGetValue(sessionId, out var username))
        {
            return GetUser(username);
        }
        return null;
    }

    public IUser? GetUser(string userName)
    {
        if (!_users.TryGetValue(userName, out var user))
        {
            return null;
        }

        return user;
    }

    public IEnumerable<IUser> GetAllUsers()
    {
        return _users.Values;
    }

    public bool IsAuthenticated(string sessionId)
    {
        return _sessionToUserMap.ContainsKey(sessionId);
    }

    public void End()
    {
        foreach (var user in _users.Values)
        {
            user.Session?.Close();
        }
    }
}
