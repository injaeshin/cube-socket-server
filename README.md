# Cube

.NET 8.0 기반 TCP/UDP 네트워크 서버 라이브러리

## 개요

Cube는 실시간 통신이 필요한 애플리케이션을 위한 네트워크 서버 라이브러리입니다. TCP와 UDP를 동시에 지원하며, 세션 관리, 패킷 처리, 메모리 풀링 등의 기능을 제공합니다.

## 주요 기능

### 네트워크 통신
- **TCP/UDP 동시 지원**: 하나의 서버에서 두 프로토콜 동시 처리
- **비동기 고성능 처리**: SocketAsyncEventArgs 기반 최적화
- **UDP 신뢰성**: ACK 메커니즘과 패킷 재전송으로 신뢰성 보장

### 세션 관리
- **연결 상태 추적**: 클라이언트 연결/해제 자동 관리
- **최대 연결 수 제한**: 설정 가능한 동시 접속 제한
- **세션 강제 종료**: 특정 클라이언트 연결 해제
- **전체 브로드캐스트**: 모든 연결된 클라이언트에 메시지 전송

### 메모리 최적화
- **객체 풀링**: 연결, 버퍼, 소켓 객체 재사용
- **메모리 효율성**: Memory<byte> 사용으로 복사 최소화
- **스레드 안전**: 동시성 환경에서 안전한 객체 관리

### 패킷 처리 시스템
- **타입 기반 라우팅**: 패킷 타입별 자동 핸들러 호출
- **비동기 처리**: async/await 기반 메시지 처리
- **확장 가능한 구조**: 새로운 패킷 타입 쉽게 추가

### 하트비트 모니터링
- **연결 상태 확인**: 주기적인 핑/퐁으로 연결 상태 점검
- **자동 재연결**: 연결 끊김 감지 및 처리
- **타임아웃 관리**: 응답 없는 클라이언트 자동 제거

## 프로젝트 구조

```
Cube/
├── Cube.Core/          # 핵심 네트워크 라이브러리
├── Cube.Packet/        # 패킷 직렬화/역직렬화
├── Cube.Server.Chat/   # 채팅 서버 구현 예제
├── Cube.Client/        # 클라이언트 구현 예제
├── Cube.Tests/         # 단위 테스트
└── Cube.Benchmark/     # 성능 벤치마크
```

## 빠른 시작

### 1. 서버 설정

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        AppSettings.Initialize(context.Configuration);
        CoreHelper.AddServices(services);
        // 사용자 정의 서비스 등록
    })
    .Build();

await host.RunAsync();
```

### 2. 패킷 핸들러 구현

```csharp
public class ChatHandler : IPacketHandler
{
    public PacketType Type => PacketType.Chat;

    public async Task<bool> HandleAsync(ISession session, ReadOnlyMemory<byte> payload)
    {
        var message = new PacketReader(payload).ReadString();
        await ProcessChatMessage(session, message);
        return true;
    }
}
```

### 3. 설정 파일 (appsettings.json)

```json
{
  "Network": {
    "MaxConnections": 1000,
    "UdpEnabled": true
  },
  "Heartbeat": {
    "Interval": 30000,
    "PingTimeout": 5000
  }
}
```

## 실행 예제

### 채팅 서버 실행
```bash
cd Cube.Server.Chat
dotnet run
```

### 클라이언트 실행
```bash
cd Cube.Client
dotnet run
```

## 성능 특징

- **객체 풀링**: 메모리 할당 최소화로 GC 압박 감소
- **비동기 I/O**: 높은 동시 처리 성능
- **메모리 효율**: 불필요한 복사 최소화
- **스레드 안전**: 멀티스레드 환경 완전 지원

## 기여

이슈 보고, 기능 제안, 풀 리퀘스트를 환영합니다.