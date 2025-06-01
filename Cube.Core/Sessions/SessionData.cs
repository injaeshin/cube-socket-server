namespace Cube.Core.Sessions;

public class SessionState
{
    public SessionStateType StateType { get; private set; }
    public void SetConnected() => StateType = SessionStateType.Connected;
    public bool IsConnected => StateType == SessionStateType.Connected;
    public void SetAuthenticated() => StateType = SessionStateType.Authenticated;
    public bool IsAuthenticated => StateType == SessionStateType.Authenticated;
    public void SetDisconnected() => StateType = SessionStateType.Disconnected;
    public bool IsDisconnected => StateType == SessionStateType.Disconnected;

    public void Clear()
    {
        StateType = SessionStateType.None;
    }
}
