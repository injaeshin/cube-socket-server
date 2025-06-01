using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Cube.Core;

namespace Cube.Server.Chat.User;

public interface IUserManager
{
    bool TryInsertUser(string username, ISession session, out IChatUser? user);
    bool DeleteUser(string sessionId);
    bool TryGetUserBySession(string sessionId, out IChatUser? user);
    bool TryGetUser(string username, out IChatUser? user);
    IEnumerable<IChatUser> GetAllUsers();
    void Stop();
}

public class UserManager : IUserManager
{
    private readonly ConcurrentDictionary<string, IChatUser> _users = new();
    private readonly ConcurrentDictionary<string, string> _sessionToUserMap = new();
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<UserManager>();

    public bool TryInsertUser(string username, ISession session, out IChatUser? user)
    {
        user = null;

        // 세션 유효성 검사
        if (session == null || string.IsNullOrEmpty(session.SessionId))
        {
            _logger.LogWarning("Invalid session");
            return false;
        }

        // 사용자명 유효성 검사
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Invalid username");
            return false;
        }

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
        user = new ChatUser(username, session);
        if (!_users.TryAdd(username, user))
        {
            _logger.LogWarning("Failed to add user: {Username}", username);
            return false;
        }

        if (!_sessionToUserMap.TryAdd(session.SessionId, username))
        {
            _users.TryRemove(username, out _);
            _logger.LogWarning("Failed to add session mapping: {SessionId}", session.SessionId);
            return false;
        }

        _logger.LogInformation("Added user: {Username}, {SessionId} / Current Users : {Users}", username, session.SessionId, _users.Count);
        return true;
    }

    public bool DeleteUser(string sessionId)
    {
        if (!_sessionToUserMap.TryRemove(sessionId, out var username))
        {
            _logger.LogWarning("Not found session: {SessionId}", sessionId);
            return false;
        }

        if (!_users.TryRemove(username, out _))
        {
            _logger.LogWarning("Not found user: {Username}", username);
            return false;
        }

        _logger.LogInformation("Removed user: {Username}, {SessionId} / Current Users : {Users}", username, sessionId, _users.Count);
        return true;
    }

    public bool TryGetUserBySession(string sessionId, out IChatUser? user)
    {
        if (!_sessionToUserMap.TryGetValue(sessionId, out var username))
        {
            user = null;
            return false;
        }

        return TryGetUser(username, out user);
    }

    public bool TryGetUser(string username, out IChatUser? user) => _users.TryGetValue(username, out user);
    public IEnumerable<IChatUser> GetAllUsers() => _users.Values;

    public void Stop()
    {
        foreach (var user in _users.Values)
        {
            user.Session.Kick(ErrorType.ApplicationRequest);
        }
    }
}
