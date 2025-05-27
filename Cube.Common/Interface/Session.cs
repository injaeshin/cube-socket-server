namespace Cube.Common.Interface;

public interface ISessionState
{
    void SetConnected();
    bool IsConnected { get; }
    void SetAuthenticated();
    bool IsAuthenticated { get; }
    void SetDisconnected();
    bool IsDisconnected { get; }
}

public interface ISession : ISessionState
{
    string SessionId { get; }

    Task SendAsync(Memory<byte> data, byte[]? rentedBuffer);

    void Close(ISessionClose reason);
}

public interface ISessionClose
{
    int Code { get; }
    string Description { get; }
    bool IsGraceful { get; }
}
