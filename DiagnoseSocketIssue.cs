using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using Common.Network;
using Common.Network.Packet;
using Common.Network.Session;
using Common.Network.Transport;

namespace DiagnoseSocketIssue
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("소켓 통신 진단 도구 시작");
            Console.WriteLine("1. 서버 모드");
            Console.WriteLine("2. 클라이언트 모드");
            Console.Write("선택: ");
            
            var input = Console.ReadLine();
            if (input == "1")
            {
                await RunServerMode();
            }
            else if (input == "2")
            {
                await RunClientMode();
            }
            else
            {
                Console.WriteLine("잘못된 선택입니다.");
            }
        }

        static async Task RunServerMode()
        {
            Console.WriteLine("서버 모드 시작...");
            
            // 로거 설정
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger<Program>();
            
            try
            {
                // 서버 소켓 생성
                var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Bind(new IPEndPoint(IPAddress.Any, Constant.PORT));
                listener.Listen(10);
                
                logger.LogInformation("서버 시작됨. 포트: {Port}", Constant.PORT);
                
                Console.WriteLine("클라이언트 연결 대기 중...");
                var clientSocket = await Task.Run(() => listener.Accept());
                
                logger.LogInformation("클라이언트 연결됨: {EndPoint}", clientSocket.RemoteEndPoint);
                
                // 세션 설정
                var session = CreateTestSession(loggerFactory);
                
                // 세션 실행
                session.Run(clientSocket);
                
                Console.WriteLine("세션 실행 중... 종료하려면 아무 키나 누르세요.");
                Console.ReadKey();
                
                // 세션 종료
                session.Close();
                listener.Close();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "서버 모드 실행 중 오류 발생");
            }
            
            Console.WriteLine("서버 모드 종료.");
        }
        
        static async Task RunClientMode()
        {
            Console.WriteLine("클라이언트 모드 시작...");
            
            // 로거 설정
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger<Program>();
            
            try
            {
                // 클라이언트 소켓 생성
                var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                Console.Write("서버 IP (기본값: 127.0.0.1): ");
                var ip = Console.ReadLine();
                if (string.IsNullOrEmpty(ip))
                {
                    ip = "127.0.0.1";
                }
                
                await clientSocket.ConnectAsync(ip, Constant.PORT);
                logger.LogInformation("서버에 연결됨: {EndPoint}", clientSocket.RemoteEndPoint);
                
                // 로그인 패킷 전송
                Console.Write("사용자 이름: ");
                var username = Console.ReadLine() ?? "TestUser";
                
                var loginPacket = BuildLoginPacket(username);
                clientSocket.Send(loginPacket);
                logger.LogInformation("로그인 패킷 전송됨: {Length} 바이트", loginPacket.Length);
                
                // 응답 대기
                var buffer = new byte[1024];
                var received = await Task.Run(() => clientSocket.Receive(buffer));
                logger.LogInformation("응답 수신됨: {Length} 바이트", received);
                
                if (received > 0)
                {
                    var responseBytes = new byte[received];
                    Buffer.BlockCopy(buffer, 0, responseBytes, 0, received);
                    LogPacketDetails(responseBytes, logger);
                }
                
                Console.WriteLine("종료하려면 아무 키나 누르세요.");
                Console.ReadKey();
                
                clientSocket.Close();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "클라이언트 모드 실행 중 오류 발생");
            }
            
            Console.WriteLine("클라이언트 모드 종료.");
        }
        
        static SocketSession CreateTestSession(ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<SocketSession>();
            
            // SocketAsyncEventArgs 준비
            var receiveArgs = new SocketAsyncEventArgs();
            var buffer = new byte[Constant.BUFFER_SIZE];
            receiveArgs.SetBuffer(buffer, 0, buffer.Length);
            
            var options = new SocketSessionOptions
            {
                Resource = new SessionResource
                {
                    OnRentRecvArgs = () => receiveArgs,
                    OnReturnRecvArgs = args => { },
                    OnReturnSession = session => { }
                },
                Queue = new SessionQueue
                {
                    OnRecvEnqueueAsync = packet => 
                    {
                        logger.LogInformation("패킷 수신됨: SessionId={SessionId}, Length={Length}", 
                            packet.SessionId, packet.Data.Length);
                        return Task.CompletedTask;
                    },
                    OnSendEnqueueAsync = request => 
                    {
                        logger.LogInformation("패킷 전송됨: SessionId={SessionId}, Length={Length}", 
                            request.SessionId, request.Data.Length);
                        return Task.CompletedTask;
                    }
                }
            };
            
            var session = new SocketSession(options, logger);
            
            // 세션 이벤트 핸들러 등록
            session.SessionConnected += (sender, e) => 
            {
                logger.LogInformation("세션 연결됨: {SessionId}", e.Session.SessionId);
            };
            
            session.SessionDisconnected += (sender, e) => 
            {
                logger.LogInformation("세션 연결 끊김: {SessionId}", e.Session.SessionId);
            };
            
            session.SessionPreProcess += (sender, e) => 
            {
                var packetType = PacketIO.GetPacketType(e.Data);
                logger.LogInformation("전처리: SessionId={SessionId}, PacketType={PacketType}, Length={Length}", 
                    e.Session.SessionId, packetType, e.Data.Length);
                
                // 로그인 패킷 처리
                if (packetType == PacketType.Login)
                {
                    var payload = PacketIO.GetPayload(e.Data);
                    var username = Encoding.UTF8.GetString(payload.Span);
                    logger.LogInformation("로그인 요청: {Username}", username);
                    
                    // 응답 전송
                    var responseMessage = $"환영합니다, {username}!";
                    e.Session.SendAsync(PacketType.LoginSuccess, Encoding.UTF8.GetBytes(responseMessage));
                }
                
                return Task.FromResult(SessionPreProcessResult.Continue);
            };
            
            return session;
        }
        
        static byte[] BuildLoginPacket(string username)
        {
            // 로그인 패킷 생성
            using var writer = new PacketWriter();
            writer.Write(username);
            var payload = writer.ToArray();
            
            // 패킷 헤더 (바디 길이 + 패킷 타입)
            ushort bodyLength = (ushort)(2 + payload.Length); // 패킷 타입(2) + 페이로드 길이
            
            var packet = new byte[2 + bodyLength]; // 길이 필드(2) + 바디 길이
            
            // 바디 길이 (빅 엔디안)
            packet[0] = (byte)(bodyLength >> 8);
            packet[1] = (byte)(bodyLength & 0xFF);
            
            // 패킷 타입 (Login = 1)
            packet[2] = 0;
            packet[3] = 1;
            
            // 페이로드 복사
            Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);
            
            return packet;
        }
        
        static void LogPacketDetails(byte[] packet, ILogger logger)
        {
            if (packet.Length < 4)
            {
                logger.LogWarning("패킷이 너무 짧습니다: {Length} 바이트", packet.Length);
                return;
            }
            
            // 패킷 구조 디코딩
            ushort bodyLength = (ushort)((packet[0] << 8) | packet[1]);
            ushort packetType = (ushort)((packet[2] << 8) | packet[3]);
            
            logger.LogInformation("패킷 정보: 바디 길이={BodyLength}, 패킷 타입={PacketType}", bodyLength, packetType);
            
            if (packet.Length < 2 + bodyLength)
            {
                logger.LogWarning("패킷 데이터가 부족합니다. 예상 길이: {Expected}, 실제 길이: {Actual}", 
                    2 + bodyLength, packet.Length);
                return;
            }
            
            // 페이로드 출력 (타입 이후의 데이터)
            if (bodyLength > 2)
            {
                var payloadLength = bodyLength - 2; // 패킷 타입 크기를 뺀 페이로드 크기
                var payload = new byte[payloadLength];
                Buffer.BlockCopy(packet, 4, payload, 0, payloadLength);
                
                // 문자열로 해석 시도
                try
                {
                    var text = Encoding.UTF8.GetString(payload);
                    logger.LogInformation("페이로드 (텍스트): {Text}", text);
                }
                catch
                {
                    logger.LogInformation("페이로드 (바이너리): {Length} 바이트", payloadLength);
                }
            }
        }
    }
} 