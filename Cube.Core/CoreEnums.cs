namespace Cube.Core;

public class CoreConsts
{
    // 네트워크 설정
    public const int LISTEN_BACKLOG = 100;
    public const int TCP_ACCEPT_POOL_SIZE = 10;
    public const int TCP_SEND_POOL_SIZE = 1000;
    public const int TCP_RECEIVE_POOL_SIZE = 1000;
    public const int MAX_CONNECTIONS = 1000;
    public const int UDP_SEND_POOL_SIZE = 50;
    public const int UDP_RECEIVE_POOL_SIZE = 50;

    // 타임아웃 및 간격
    public const int HEARTBEAT_INTERVAL = 15;    // 간격 (초)
    public const int PING_TIMEOUT = 5;           // 타임아웃 (초)
    public const int RESEND_INTERVAL_MS = 300;      // 간격 (밀리초)
}

public enum ErrorType
{
    None,                  // 종료 상태 아님 (기본값)
    GracefulDisconnect,    // 정상적인 종료 (클라이언트의 요청)
    ProcessFailed,         // 로직 실패
    SocketError,           // 소켓 오류
    ApplicationRequest,    // 애플리케이션에서 요청
    InvalidType,           // 잘못된 타입
    InvalidData,           // 잘못된 데이터
    InvalidPacket,         // 잘못된 패킷
    NotConnected,          // 연결되지 않음
    NotAuthorized,         // 권한 없음
    NotAuthenticated,      // 인증되지 않음
    NotFound,              // 찾을 수 없음
    Timeout,               // 시간 초과
    ServerShutdown         // 서버 종료
}