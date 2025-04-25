namespace Common.Network;

// 종료 유형을 표현하는 열거형 추가 (인터페이스 레벨로 이동)
public enum DisconnectReason
{
    None,                  // 종료 상태 아님 (기본값)
    GracefulDisconnect,    // 정상적인 종료 (클라이언트의 요청)
    SocketError,           // 소켓 오류
    ApplicationRequest,    // 애플리케이션에서 요청
    InvalidData,           // 잘못된 데이터
    Timeout,               // 시간 초과
    ServerShutdown         // 서버 종료
}