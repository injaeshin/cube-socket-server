namespace Cube.Network;


// TransportType 은 비트 연산으로 처리함
// 예) TransportType.Tcp | TransportType.Udp
[Flags]
public enum TransportType
{
    Tcp = 1 << 0,
    Udp = 1 << 1,
    WebSocket = 1 << 2,
}

public enum DisconnectReason
{
    None,                  // 종료 상태 아님 (기본값)
    Failed,                // 실패
    GracefulDisconnect,    // 정상적인 종료 (클라이언트의 요청)
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