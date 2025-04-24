namespace Common.Network.Session;

/// <summary>
/// 세션 이벤트 처리 결과를 나타내는 열거형
/// </summary>
public enum SessionPreProcessResult
{
    /// <summary>
    /// 패킷 처리를 계속하고 큐에 추가합니다.
    /// </summary>
    Continue,
    
    /// <summary>
    /// 패킷 처리를 중단하고 버퍼를 반환합니다. 큐에 추가하지 않습니다.
    /// </summary>
    Discard,
    
    /// <summary>
    /// 패킷 처리를 완료했으므로 큐에 추가하지 않고 다음 패킷으로 진행합니다.
    /// </summary>
    Handled
}

public class SessionEventArgs(ISession session) : EventArgs
{
    public ISession Session { get; } = session;
}

public class SessionDataEventArgs(ISession session, ReadOnlyMemory<byte> data) : SessionEventArgs(session)
{
    public ReadOnlyMemory<byte> Data { get; } = data;
}

/// <summary>
/// 세션 데이터 전처리 이벤트 핸들러 델리게이트.
/// </summary>
/// <param name="sender">이벤트 발생 객체</param>
/// <param name="e">세션 데이터 이벤트 인자</param>
/// <returns>패킷 처리 결과</returns>
public delegate Task<SessionPreProcessResult> SessionDataEventHandlerAsync(object? sender, SessionDataEventArgs e);
