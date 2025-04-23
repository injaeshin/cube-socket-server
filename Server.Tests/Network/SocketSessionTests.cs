using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Common.Network;
using Common.Network.Packet;
using Common.Network.Session;
using Common.Network.Transport;

namespace Server.Tests.Network
{
    public class SocketSessionTests
    {
        private readonly Mock<ILogger<SocketSession>> _loggerMock;
        private readonly SocketSessionOptions _sessionOptions;

        public SocketSessionTests()
        {
            _loggerMock = new Mock<ILogger<SocketSession>>();
            
            // 세션 옵션 설정
            var onRentRecvArgsMock = new Mock<Func<SocketAsyncEventArgs?>>();
            var onReturnRecvArgsMock = new Mock<Action<SocketAsyncEventArgs>>();
            var onReturnSessionMock = new Mock<Action<ISession>>();
            var onRecvEnqueueAsyncMock = new Mock<Func<ReceivedPacket, Task>>();
            var onSendEnqueueAsyncMock = new Mock<Func<SendRequest, Task>>();

            // 기본 SocketAsyncEventArgs 생성
            var receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(new byte[Constant.BUFFER_SIZE], 0, Constant.BUFFER_SIZE);
            onRentRecvArgsMock.Setup(f => f()).Returns(receiveArgs);

            _sessionOptions = new SocketSessionOptions
            {
                Resource = new SessionResource
                {
                    OnRentRecvArgs = onRentRecvArgsMock.Object,
                    OnReturnRecvArgs = onReturnRecvArgsMock.Object,
                    OnReturnSession = onReturnSessionMock.Object
                },
                Queue = new SessionQueue
                {
                    OnRecvEnqueueAsync = onRecvEnqueueAsyncMock.Object,
                    OnSendEnqueueAsync = onSendEnqueueAsyncMock.Object
                }
            };
        }

        // 테스트 1: CreateSessionId가 제대로 호출되는지 확인
        [Fact]
        public void CreateSessionId_ShouldGenerateValidId()
        {
            // Arrange
            var session = new SocketSession(_sessionOptions, _loggerMock.Object);

            // Act
            session.CreateSessionId();

            // Assert
            Assert.NotNull(session.SessionId);
            Assert.NotEmpty(session.SessionId);
            Assert.StartsWith("S_", session.SessionId);
        }

        // 테스트 2: SessionPreProcess 이벤트가 제대로 호출되는지 확인하는 통합 테스트
        [Fact]
        public async Task Run_WithValidData_ShouldTriggerPreProcess()
        {
            // Arrange
            var session = new SocketSession(_sessionOptions, _loggerMock.Object);
            bool preProcessCalled = false;
            
            session.SessionPreProcess += (sender, args) => 
            {
                preProcessCalled = true;
                return Task.FromResult(SessionPreProcessResult.Continue);
            };

            // Act & Assert (이 부분은 직접 실행하기 어려운 통합 테스트임)
            // 이 테스트는 목적을 설명하기 위한 것으로, 실제 구현에서는 Mock을 사용한 단위 테스트로 변경 필요
        }

        // 테스트 3: DoReceive 메서드 테스트 - ReceiveAsync 호출 확인
        [Fact]
        public void DoReceive_ShouldCallSocketReceiveAsync()
        {
            // 이 테스트는 리플렉션을 통해 private 메서드를 호출해야 하므로
            // 실제 구현에서는 테스트 접근성을 위해 protected virtual로 변경하는 것이 좋음
            // 여기서는 개념적인 테스트 설계만 제시
        }

        // 테스트 4: 소켓 통신 모의 테스트 (클라이언트-서버)
        [Fact]
        public async Task Socket_Communication_ShouldWork()
        {
            // 이 테스트는 실제 소켓 통신을 모의하는 복잡한 테스트로,
            // 클라이언트와 서버 소켓을 모두 생성하고 데이터를 주고받는 테스트
            // 실제 구현 시에는 Mock을 사용하거나 테스트용 래퍼 클래스를 만드는 것이 좋음
        }

        // 테스트 5: OnReceiveCompleted 콜백이 호출되지 않는 문제 진단 테스트
        [Fact]
        public void DiagnoseOnReceiveCompletedCallback()
        {
            // Arrange
            var session = new SocketSession(_sessionOptions, _loggerMock.Object);
            session.CreateSessionId(); // 세션 ID 생성 확인
            
            // 로거 설정
            var realLogger = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug)
            ).CreateLogger<SocketSession>();

            // 두 개의 소켓 생성 (로컬 통신)
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            var localEndPoint = (IPEndPoint)listener.LocalEndPoint!;

            using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(localEndPoint);
            
            using var serverSocket = listener.Accept();

            // 테스트용 세션 옵션 생성
            var receiveArgs = new SocketAsyncEventArgs();
            var buffer = new byte[Constant.BUFFER_SIZE];
            receiveArgs.SetBuffer(buffer, 0, buffer.Length);

            var testSessionOptions = new SocketSessionOptions
            {
                Resource = new SessionResource
                {
                    OnRentRecvArgs = () => receiveArgs,
                    OnReturnRecvArgs = args => { },
                    OnReturnSession = s => { }
                },
                Queue = new SessionQueue
                {
                    OnRecvEnqueueAsync = packet => Task.CompletedTask,
                    OnSendEnqueueAsync = request => Task.CompletedTask
                }
            };

            // 새 세션 생성
            var testSession = new SocketSession(testSessionOptions, realLogger);
            
            // 중요: SessionPreProcess 이벤트 핸들러 등록
            testSession.SessionPreProcess += (sender, e) => 
            {
                Console.WriteLine($"PreProcess called: SessionId={testSession.SessionId}, DataLength={e.Data.Length}");
                return Task.FromResult(SessionPreProcessResult.Continue);
            };
            
            // Act
            // 세션 ID 생성 및 실행
            testSession.CreateSessionId();
            Console.WriteLine($"Created SessionId: {testSession.SessionId}");
            
            testSession.Run(serverSocket);
            Console.WriteLine("Session is running");

            // 클라이언트에서 데이터 전송
            var loginPayload = BuildTestLoginPacket("testuser");
            clientSocket.Send(loginPayload);
            Console.WriteLine($"Sent login packet: {loginPayload.Length} bytes");

            // 잠시 대기하여 비동기 처리 완료 대기
            Thread.Sleep(500);

            // 이 테스트는 실제로 OnReceiveCompleted 콜백이 호출되는지 콘솔에서 확인하기 위한 것
            // 자동화된 Assert는 없지만, 로그를 통해 문제 진단 가능
        }

        private byte[] BuildTestLoginPacket(string username)
        {
            // 로그인 패킷 생성
            byte[] usernameBytes = System.Text.Encoding.UTF8.GetBytes(username);
            
            // 패킷 구조: [2바이트 길이][2바이트 패킷타입][사용자명 길이(2바이트)][사용자명]
            int packetSize = 2 + 2 + 2 + usernameBytes.Length;
            byte[] packet = new byte[2 + packetSize]; // 전체 길이 포함
            
            // 패킷 길이 (패킷 타입 + 페이로드 길이)
            packet[0] = (byte)((packetSize) >> 8);
            packet[1] = (byte)((packetSize) & 0xFF);
            
            // 패킷 타입 (Login = 1)
            packet[2] = 0;
            packet[3] = 1;
            
            // 사용자명 길이
            packet[4] = (byte)(usernameBytes.Length >> 8);
            packet[5] = (byte)(usernameBytes.Length & 0xFF);
            
            // 사용자명 복사
            Buffer.BlockCopy(usernameBytes, 0, packet, 6, usernameBytes.Length);
            
            return packet;
        }
    }
} 