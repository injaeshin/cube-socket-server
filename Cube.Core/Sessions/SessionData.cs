using Cube.Common.Interface;

namespace Cube.Core.Sessions;

public class SessionState
{
    private ISession _session = null!;
    public ISession Session => _session;

    public string SessionId => _session.SessionId;

    public SessionStateType StateType { get; private set; }
    public void SetConnected() => StateType = SessionStateType.Connected;
    public bool IsConnected => StateType == SessionStateType.Connected;
    public void SetAuthenticated() => StateType = SessionStateType.Authenticated;
    public bool IsAuthenticated => StateType == SessionStateType.Authenticated;
    public void SetDisconnected() => StateType = SessionStateType.Disconnected;
    public bool IsDisconnected => StateType == SessionStateType.Disconnected;

    public void Clear()
    {
        _session = null!;
        StateType = SessionStateType.None;
    }
}
