// using System.Net;
// using System.Net.Sockets;
// using Common.Network;
// using Microsoft.Extensions.Logging;
// using Xunit;
// using Xunit.Abstractions;

// namespace Common.Tests.Network;

// public class SocketAcceptorTests
// {
//     private readonly ITestOutputHelper _testOutputHelper;
//     private readonly ILogger<SocketAcceptor> _logger;

//     public SocketAcceptorTests(ITestOutputHelper testOutputHelper)
//     {
//         _testOutputHelper = testOutputHelper;
//         // Xunit 테스트 출력 로거 생성
//         var loggerFactory = LoggerFactory.Create(builder => 
//         {
//             builder.AddConsole();
//             builder.AddProvider(new XunitLoggerProvider(testOutputHelper));
//         });
//         _logger = loggerFactory.CreateLogger<SocketAcceptor>();
//     }

//     private static int GetAvailablePort()
//     {
//         // 사용 가능한 포트 찾기
//         using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
//         {
//             socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
//             return ((IPEndPoint)socket.LocalEndPoint!).Port;
//         }
//     }

//     [Fact]
//     public async Task MassiveAcceptTest()
//     {
//         // 설정
//         int testPort = GetAvailablePort();
//         int connectionCount = 200; // 테스트할 연결 수
//         int maxConnections = 1000; // 최대 연결 대기 큐 사이즈
//         int acceptedClients = 0;
//         var clientConnectedEvent = new TaskCompletionSource<bool>();
//         var acceptedClientsLock = new object();

//         // Acceptor 설정
//         Task onClientConnected(Socket socket)
//         {
//             lock (acceptedClientsLock)
//             {
//                 acceptedClients++;
                
//                 if (acceptedClients >= connectionCount)
//                 {
//                     clientConnectedEvent.TrySetResult(true);
//                 }
//             }
            
//             // 연결된 소켓 정리
//             try
//             {
//                 socket.Close();
//             }
//             catch (Exception)
//             {
//                 // 무시
//             }
            
//             return Task.CompletedTask;
//         }

//         // 서버 시작
//         var acceptor = new SocketAcceptor(_logger, onClientConnected, testPort, maxConnections);
//         var serverTask = acceptor.Begin();

//         _testOutputHelper.WriteLine($"서버가 포트 {testPort}에서 시작됨 (최대 연결 대기 큐: {maxConnections})");

//         try
//         {
//             // 클라이언트 연결 시작
//             var clientTasks = new List<Task<bool>>();
//             for (int i = 0; i < connectionCount; i++)
//             {
//                 clientTasks.Add(Task.Run(() =>
//                 {
//                     try
//                     {
//                         using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//                         client.Connect(IPAddress.Loopback, testPort);
//                         // 짧은 대기 (소켓이 처리될 시간 제공)
//                         Thread.Sleep(10);
//                         // 소켓 닫기
//                         client.Close();
//                         return true;
//                     }
//                     catch (Exception ex)
//                     {
//                         _testOutputHelper.WriteLine($"클라이언트 연결 실패: {ex.Message}");
//                         return false;
//                     }
//                 }));
//             }

//             // 한꺼번에 클라이언트 연결 시도
//             _testOutputHelper.WriteLine($"{connectionCount}개의 클라이언트 연결 시도 중...");
//             var clientResults = await Task.WhenAll(clientTasks);
            
//             // 성공적으로 연결된 클라이언트 수
//             int successfulConnections = clientResults.Count(r => r);
//             _testOutputHelper.WriteLine($"연결 시도 결과: {successfulConnections}/{connectionCount} 성공");

//             // 모든 클라이언트가 접속될 때까지 대기 (최대 10초)
//             var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
//             await Task.WhenAny(clientConnectedEvent.Task, timeoutTask);

//             if (timeoutTask.IsCompleted)
//             {
//                 _testOutputHelper.WriteLine("시간 초과: 일부 클라이언트가 서버에서 처리되지 않았습니다.");
//             }
            
//             _testOutputHelper.WriteLine($"서버에서 처리된 클라이언트: {acceptedClients}/{connectionCount}");
//             Assert.Equal(successfulConnections, acceptedClients);
//         }
//         finally
//         {
//             // 서버 종료
//             acceptor.End();
//         }
//     }
    
//     [Fact]
//     public async Task StressTestWithHighConnectionRate()
//     {
//         // 설정
//         int testPort = GetAvailablePort();
//         int connectionCount = 500; // 테스트할 연결 수
//         int maxConnections = 1000; // 최대 연결 대기 큐 사이즈
//         int acceptedClients = 0;
//         var clientConnectedEvent = new TaskCompletionSource<bool>();
//         var acceptedClientsLock = new object();

//         // Acceptor 설정
//         Task onClientConnected(Socket socket)
//         {
//             lock (acceptedClientsLock)
//             {
//                 acceptedClients++;
                
//                 if (acceptedClients >= connectionCount)
//                 {
//                     clientConnectedEvent.TrySetResult(true);
//                 }
//             }
            
//             // 연결된 소켓 정리 (약간의 지연 추가)
//             try
//             {
//                 // 서버 측에서 소켓 처리에 부하를 주기 위해 작은 지연 추가
//                 Thread.Sleep(1); 
//                 socket.Close();
//             }
//             catch (Exception)
//             {
//                 // 무시
//             }
            
//             return Task.CompletedTask;
//         }

//         // 서버 시작
//         var acceptor = new SocketAcceptor(_logger, onClientConnected, testPort, maxConnections);
//         var serverTask = acceptor.Begin();

//         _testOutputHelper.WriteLine($"고부하 테스트: 서버가 포트 {testPort}에서 시작됨 (최대 연결 대기 큐: {maxConnections})");

//         try
//         {
//             // 클라이언트 연결 시작 (더 적은 간격으로 빠르게 연결)
//             var clientTasks = new List<Task<bool>>();
//             for (int i = 0; i < connectionCount; i++)
//             {
//                 var taskIndex = i; // 클로저에서 사용하기 위해 캡처
                
//                 clientTasks.Add(Task.Run(() =>
//                 {
//                     try
//                     {
//                         using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        
//                         // 연결 타임아웃 설정 (기본값인 무한 대기를 피하기 위해)
//                         client.ReceiveTimeout = 5000;
//                         client.SendTimeout = 5000;
                        
//                         // 로컬에서 매우 빠르게 연결할 수 있으므로 모든 연결이 동시에 시도되도록 함
//                         client.Connect(IPAddress.Loopback, testPort);
                        
//                         // 간단한 데이터 송수신 테스트 (선택 사항)
//                         if (taskIndex % 10 == 0) // 10개 중 1개만 데이터 송수신 테스트
//                         {
//                             byte[] dataToSend = new byte[32]; // 간단한 데이터
//                             client.Send(dataToSend);
                            
//                             byte[] receiveBuffer = new byte[32];
//                             try
//                             {
//                                 client.Receive(receiveBuffer, SocketFlags.None); // 응답이 없어도 괜찮음
//                             }
//                             catch (SocketException)
//                             {
//                                 // 서버가 응답하지 않아도 테스트 목적에는 문제 없음
//                             }
//                         }
                        
//                         // 소켓 닫기
//                         client.Close();
//                         return true;
//                     }
//                     catch (SocketException ex)
//                     {
//                         // 연결 실패 로그
//                         _testOutputHelper.WriteLine($"클라이언트 {taskIndex} 연결 실패: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
//                         return false;
//                     }
//                     catch (Exception ex)
//                     {
//                         _testOutputHelper.WriteLine($"클라이언트 {taskIndex} 예외 발생: {ex.Message}");
//                         return false;
//                     }
//                 }));
                
//                 // 연결 부하를 더 높이기 위해 (거의) 동시에 모든 연결 시도
//                 if (i % 50 == 0)
//                 {
//                     await Task.Delay(1); // 50개 연결마다 1ms 대기
//                 }
//             }

//             // 모든 클라이언트 연결 시도 대기
//             _testOutputHelper.WriteLine($"고부하 테스트: {connectionCount}개의 클라이언트 연결 시도 중...");
//             var clientResults = await Task.WhenAll(clientTasks);
            
//             // 성공적으로 연결된 클라이언트 수
//             int successfulConnections = clientResults.Count(r => r);
//             _testOutputHelper.WriteLine($"고부하 테스트 연결 결과: {successfulConnections}/{connectionCount} 성공");

//             // 모든 클라이언트가 서버에서 처리될 때까지 대기 (최대 20초)
//             var timeoutTask = Task.Delay(TimeSpan.FromSeconds(20));
//             await Task.WhenAny(clientConnectedEvent.Task, timeoutTask);

//             if (timeoutTask.IsCompleted)
//             {
//                 _testOutputHelper.WriteLine($"시간 초과: {acceptedClients}/{connectionCount} 클라이언트만 서버에서 처리됨");
//             }
//             else
//             {
//                 _testOutputHelper.WriteLine($"고부하 테스트: 모든 클라이언트가 성공적으로 처리됨");
//             }
            
//             // 성공 여부 확인 - 실제 성공한 연결만큼 서버에서 처리되었는지
//             Assert.Equal(successfulConnections, acceptedClients);
//         }
//         finally
//         {
//             // 서버 종료
//             acceptor.End();
//         }
//     }

//     //[Fact(Skip="리소스 사용이 많은 테스트입니다. 필요할 때만 실행하세요.")]
//     [Fact]
//     public async Task ExtremeStressTest()
//     {
//         // 설정
//         int testPort = GetAvailablePort();
//         int connectionCount = 1000;
//         int maxConnections = 1000; // 최대 연결 대기 큐 사이즈
//         int acceptedClients = 0;
//         var clientConnectedEvent = new TaskCompletionSource<bool>();
//         var acceptedClientsLock = new object();

//         // Acceptor 설정
//         Task onClientConnected(Socket socket)
//         {
//             lock (acceptedClientsLock)
//             {
//                 acceptedClients++;
                
//                 if (acceptedClients >= connectionCount)
//                 {
//                     clientConnectedEvent.TrySetResult(true);
//                 }
//             }
            
//             // 연결된 소켓 정리 (약간의 지연 추가)
//             try
//             {
//                 // 극단적인 부하 테스트에서 서버 부하 시뮬레이션
//                 if (acceptedClients % 200 == 0)  // 매 200번째 연결마다 더 오래 대기
//                 {
//                     Thread.Sleep(5);
//                 }
//                 socket.Close();
//             }
//             catch (Exception)
//             {
//                 // 무시
//             }
            
//             return Task.CompletedTask;
//         }

//         // 서버 시작
//         var acceptor = new SocketAcceptor(_logger, onClientConnected, testPort, maxConnections);
//         var serverTask = acceptor.Begin();

//         _testOutputHelper.WriteLine($"극한 부하 테스트: 서버가 포트 {testPort}에서 시작됨 (최대 연결 대기 큐: {maxConnections})");

//         try
//         {
//             // 병렬 처리 그룹 생성 - 대량의 연결을 동시에 진행하기 위함
//             const int parallelGroups = 10;
//             const int connectionsPerGroup = 200; // 2000 / 10
            
//             var groupTasks = new List<Task<int>>();
            
//             for (int group = 0; group < parallelGroups; group++)
//             {
//                 int groupId = group;
//                 groupTasks.Add(Task.Run(async () => 
//                 {
//                     _testOutputHelper.WriteLine($"그룹 {groupId} 시작: {connectionsPerGroup}개 연결 시도");
//                     var clientTasks = new List<Task<bool>>();
                    
//                     for (int i = 0; i < connectionsPerGroup; i++)
//                     {
//                         int connectionId = (groupId * connectionsPerGroup) + i;
                        
//                         clientTasks.Add(Task.Run(() =>
//                         {
//                             try
//                             {
//                                 using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                                
//                                 // 연결 타임아웃 및 버퍼 설정
//                                 client.ReceiveTimeout = 3000;
//                                 client.SendTimeout = 3000;
//                                 client.ReceiveBufferSize = 4096;
//                                 client.SendBufferSize = 4096;
                                
//                                 // 빠른 재사용 설정
//                                 client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                                
//                                 // 연결
//                                 client.Connect(IPAddress.Loopback, testPort);
                                
//                                 // 간단한 데이터 전송 (일부 연결만)
//                                 if (connectionId % 20 == 0)
//                                 {
//                                     byte[] dataToSend = new byte[16];
//                                     new Random().NextBytes(dataToSend);
//                                     client.Send(dataToSend);
//                                 }
                                
//                                 // 연결 유지 시간 짧게
//                                 Thread.Sleep(1);
                                
//                                 // 소켓 닫기
//                                 client.Close();
//                                 return true;
//                             }
//                             catch (SocketException ex)
//                             {
//                                 _testOutputHelper.WriteLine($"연결 {connectionId} 실패: {ex.SocketErrorCode}");
//                                 return false;
//                             }
//                             catch (Exception ex)
//                             {
//                                 _testOutputHelper.WriteLine($"연결 {connectionId} 예외: {ex.Message}");
//                                 return false;
//                             }
//                         }));
                        
//                         // 연결 요청 사이에 아주 작은 지연을 둠
//                         if (i % 20 == 0)
//                         {
//                             await Task.Delay(1);
//                         }
//                     }
                    
//                     var results = await Task.WhenAll(clientTasks);
//                     int successCount = results.Count(r => r);
//                     _testOutputHelper.WriteLine($"그룹 {groupId} 완료: {successCount}/{connectionsPerGroup} 성공");
                    
//                     return successCount;
//                 }));
//             }
            
//             // 모든 그룹 완료 대기
//             _testOutputHelper.WriteLine($"극한 부하 테스트: 총 {connectionCount}개 연결 시도 중...");
//             var groupResults = await Task.WhenAll(groupTasks);
//             int totalSuccessfulConnections = groupResults.Sum();
            
//             _testOutputHelper.WriteLine($"극한 부하 테스트 연결 결과: {totalSuccessfulConnections}/{connectionCount} 성공");
            
//             // 서버에서 모든 연결이 처리될 때까지 대기 (최대 30초)
//             var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
//             await Task.WhenAny(clientConnectedEvent.Task, timeoutTask);
            
//             if (timeoutTask.IsCompleted)
//             {
//                 _testOutputHelper.WriteLine($"시간 초과: 서버에서 {acceptedClients}/{connectionCount} 클라이언트만 처리됨");
//             }
//             else
//             {
//                 _testOutputHelper.WriteLine($"극한 부하 테스트: 모든 클라이언트가 서버에서 처리됨");
//             }
            
//             // 테스트 결과 검증 - 연결 성공한 수만큼 서버에서 처리되었는지
//             Assert.True(acceptedClients > 0, "서버에서 처리된 클라이언트가 없습니다");
//             _testOutputHelper.WriteLine($"성공한 연결 수: {totalSuccessfulConnections}, 서버에서 처리된 연결 수: {acceptedClients}");
//             Assert.Equal(totalSuccessfulConnections, acceptedClients);
//         }
//         finally
//         {
//             // 서버 종료
//             acceptor.End();
//         }
//     }
// }

// // Xunit에서 로그를 출력하기 위한 로거 프로바이더
// public class XunitLoggerProvider : ILoggerProvider
// {
//     private readonly ITestOutputHelper _testOutputHelper;

//     public XunitLoggerProvider(ITestOutputHelper testOutputHelper)
//     {
//         _testOutputHelper = testOutputHelper;
//     }

//     public ILogger CreateLogger(string categoryName)
//     {
//         return new XunitLogger(_testOutputHelper, categoryName);
//     }

//     public void Dispose()
//     {
//     }
// }

// public class XunitLogger : ILogger
// {
//     private readonly ITestOutputHelper _testOutputHelper;
//     private readonly string _categoryName;

//     public XunitLogger(ITestOutputHelper testOutputHelper, string categoryName)
//     {
//         _testOutputHelper = testOutputHelper;
//         _categoryName = categoryName;
//     }

//     public IDisposable BeginScope<TState>(TState state) where TState : notnull
//     {
//         return new DummyScope();
//     }

//     public bool IsEnabled(LogLevel logLevel)
//     {
//         return true;
//     }

//     public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
//     {
//         try
//         {
//             _testOutputHelper.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
//             if (exception != null)
//             {
//                 _testOutputHelper.WriteLine($"Exception: {exception}");
//             }
//         }
//         catch
//         {
//             // 테스트 컨텍스트가 이미 종료되었을 수 있음, 무시
//         }
//     }

//     private class DummyScope : IDisposable
//     {
//         public void Dispose()
//         {
//         }
//     }
// } 