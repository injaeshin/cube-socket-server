// using System.Net;
// using System.Net.Sockets;
// using Common.Network;
// using Common.Network.Packet;
// using Common.Network.Pool;
// using Common.Network.Session;
// using Common.Network.Transport;
// using Common.Tests.Network.Models;
// using Microsoft.Extensions.Logging;
// using Moq;
// using Xunit;
// using Xunit.Abstractions;

// namespace Common.Tests.Network.Session;

// public class SocketSessionTests
// {
//     private readonly ITestOutputHelper _testOutputHelper;
//     private readonly Mock<ILogger<SocketSession>> _loggerMock;
//     private readonly SocketSessionOptions _sessionOptions;

//     public SocketSessionTests(ITestOutputHelper testOutputHelper)
//     {
//         _testOutputHelper = testOutputHelper;
//         _loggerMock = new Mock<ILogger<SocketSession>>();

//         // 실제 객체 생성 (모킹 없이)
//         _sessionOptions = new SocketSessionOptions
//         {
//             Resource = new SessionResource
//             {
//                 OnRentRecvArgs = () => new SocketAsyncEventArgs(), // null 대신 실제 인스턴스 반환
//                 OnReturnRecvArgs = args => { },
//                 OnReturnSession = session => { }
//             },
//             Queue = new SessionQueue
//             {
//                 OnRecvEnqueueAsync = packet => Task.CompletedTask,
//                 OnSendEnqueueAsync = request => Task.CompletedTask
//             }
//         };
//     }

//     [Fact]
//     public void CreateSessionId_GeneratesUniqueIds()
//     {
//         // Arrange
//         var session1 = new SocketSession(_sessionOptions, _loggerMock.Object);
//         var session2 = new SocketSession(_sessionOptions, _loggerMock.Object);

//         // Act
//         session1.CreateSessionId();
//         session2.CreateSessionId();

//         // Assert
//         Assert.NotNull(session1.SessionId);
//         Assert.NotNull(session2.SessionId);
//         Assert.NotEqual(session1.SessionId, session2.SessionId);

//         // ID 형식 검증 (S_XXXX_XXXX)
//         Assert.StartsWith("S", session1.SessionId);
//         Assert.Equal(11, session1.SessionId.Length);

//         _testOutputHelper.WriteLine($"생성된 세션 ID 1: {session1.SessionId}");
//         _testOutputHelper.WriteLine($"생성된 세션 ID 2: {session2.SessionId}");
//     }

//     [Fact]
//     public void CreateSessionId_FormatIsCorrect()
//     {
//         // Arrange
//         var session = new SocketSession(_sessionOptions, _loggerMock.Object);

//         // Act
//         session.CreateSessionId();

//         // Assert
//         string[] parts = session.SessionId.Split('_');

//         Assert.Equal(3, parts.Length);
//         Assert.Equal("S", parts[0]);
//         Assert.Equal(4, parts[1].Length); // 시간 부분 4자리
//         Assert.Equal(4, parts[2].Length); // 유니크 부분 4자리

//         _testOutputHelper.WriteLine($"세션 ID: {session.SessionId}");
//         _testOutputHelper.WriteLine($"형식: {parts[0]}_{parts[1]}_{parts[2]}");
//     }

//     [Fact]
//     public void MultipleSessionBuffers_AreIndependent()
//     {
//         // Arrange
//         var session1 = new SocketSession(_sessionOptions, _loggerMock.Object);
//         var session2 = new SocketSession(_sessionOptions, _loggerMock.Object);
        
//         session1.CreateSessionId();
//         session2.CreateSessionId();
        
//         // 두 세션의 패킷 버퍼에 다른 데이터 추가를 시뮬레이션하기 위한 반사(reflection) 사용
//         var bufferField1 = typeof(SocketSession)
//             .GetField("_packetBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//         var packetBuffer1 = bufferField1?.GetValue(session1);
        
//         var bufferField2 = typeof(SocketSession)
//             .GetField("_packetBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//         var packetBuffer2 = bufferField2?.GetValue(session2);
        
//         // Act & Assert
//         Assert.NotNull(packetBuffer1);
//         Assert.NotNull(packetBuffer2);
        
//         // 버퍼가 같은 인스턴스를 참조하지 않는지 확인
//         Assert.NotSame(packetBuffer1, packetBuffer2);
        
//         // 세션 1의 버퍼 데이터 추가
//         var appendMethod1 = packetBuffer1?.GetType().GetMethod("Append", new[] { typeof(ReadOnlyMemory<byte>) });
//         var testData1 = new byte[] { 0, 10, 1, 2, 3 }; // 간단한 패킷 데이터 (길이 + 내용)
//         var result1 = appendMethod1?.Invoke(packetBuffer1, new object[] { new ReadOnlyMemory<byte>(testData1) });
        
//         // 세션 2의 버퍼 다른 데이터 추가
//         var appendMethod2 = packetBuffer2?.GetType().GetMethod("Append", new[] { typeof(ReadOnlyMemory<byte>) });
//         var testData2 = new byte[] { 0, 5, 5, 6, 7, 8, 9 }; // 다른 패킷 데이터
//         var result2 = appendMethod2?.Invoke(packetBuffer2, new object[] { new ReadOnlyMemory<byte>(testData2) });
        
//         // 버퍼 내용이 서로 다른지 확인하기 위해 내부 상태 검사
//         var dataSize1 = packetBuffer1?.GetType().GetMethod("DataSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//         var dataSize2 = packetBuffer2?.GetType().GetMethod("DataSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
//         var size1 = dataSize1?.Invoke(packetBuffer1, null);
//         var size2 = dataSize2?.Invoke(packetBuffer2, null);
        
//         // 데이터 크기가 다른지 확인
//         Assert.NotEqual(size1, size2);
        
//         _testOutputHelper.WriteLine($"세션1 버퍼 데이터 크기: {size1}");
//         _testOutputHelper.WriteLine($"세션2 버퍼 데이터 크기: {size2}");
        
//         // 버퍼 리셋 후에도 서로 영향을 주지 않는지 검사
//         var resetMethod1 = packetBuffer1?.GetType().GetMethod("Reset");
//         resetMethod1?.Invoke(packetBuffer1, null);
        
//         var newSize1 = dataSize1?.Invoke(packetBuffer1, null);
//         var newSize2 = dataSize2?.Invoke(packetBuffer2, null);
        
//         // 세션1만 리셋되고 세션2는 여전히 데이터가 있어야 함
//         Assert.Equal(0, Convert.ToInt32(newSize1));
//         Assert.NotEqual(0, Convert.ToInt32(newSize2));
        
//         _testOutputHelper.WriteLine($"세션1 리셋 후 버퍼 크기: {newSize1}");
//         _testOutputHelper.WriteLine($"세션2 리셋 없이 버퍼 크기: {newSize2}");
//     }

//     // 소켓 통신 테스트는 복잡하므로 비활성화
//     // 실제 테스트는 통합 테스트나 모킹을 더 정교하게 해서 구현
//     [Fact(Skip = "소켓 통신을 포함한 복잡한 테스트이므로 비활성화")]
//     public void Session_EventsAreTriggered()
//     {
//         // 이 테스트는 실제 소켓 통신이 필요하여 복잡하므로
//         // 단위 테스트보다는 통합 테스트에서 진행하는 것이 더 적합함
//         _testOutputHelper.WriteLine("이 테스트는 비활성화되었습니다");
//     }

//     //[Fact]
//     //public async Task Session_DataReceived_CallsHandler()
//     //{
//     //    // Arrange
//     //    var session = new SocketSession(_sessionOptions, _loggerMock.Object);
//     //    bool dataReceivedHandlerCalled = false;

//     //    session.SessionDataReceived += (sender, args) =>
//     //    {
//     //        dataReceivedHandlerCalled = true;
//     //        _testOutputHelper.WriteLine($"데이터 수신 이벤트 발생: {args.Packet.PacketType}");
//     //        return Task.FromResult(true);
//     //    };

//     //    session.CreateSessionId();

//     //    // Act
//     //    using var socket = SetupMockSocket();
//     //    session.Run(socket);

//     //    // 패킷 수신 시뮬레이션
//     //    var packet = new Packet(PacketType.Chat, new byte[] { 1, 2, 3 });
//     //    await SimulatePacketReceived(session, packet);

//     //    // Assert
//     //    Assert.True(dataReceivedHandlerCalled, "데이터 수신 핸들러가 호출되지 않았습니다");

//     //    // Clean up
//     //    session.Close();
//     //}

//     //[Fact]
//     //public async Task SendAsync_EnqueuesPacket()
//     //{
//     //    // Arrange
//     //    var session = new SocketSession(_sessionOptions, _loggerMock.Object);
//     //    session.CreateSessionId();

//     //    using var socket = SetupMockSocket();
//     //    session.Run(socket);

//     //    var payload = new byte[] { 1, 2, 3, 4 };
//     //    var enqueueCalled = false;

//     //    _queueMock.Setup(q => q.OnSendEnqueueAsync).Returns(new Func<SendRequest, Task>(request =>
//     //    {
//     //        enqueueCalled = true;
//     //        return Task.CompletedTask;
//     //    }));

//     //    // Act
//     //    await session.SendAsync(PacketType.Chat, payload);

//     //    // Assert
//     //    Assert.True(enqueueCalled, "SendAsync가 큐에 패킷을 추가하지 않았습니다");
//     //    _queueMock.Verify(q => q.OnSendEnqueueAsync(It.IsAny<SendRequest>()), Times.Once);

//     //    // Clean up
//     //    session.Close();
//     //}

//     [Fact]
//     public void MultipleSessionsDataHandling_DoesNotInterfere()
//     {
//         // Arrange - 두 개의 세션 준비
//         var session1 = new SocketSession(_sessionOptions, _loggerMock.Object);
//         var session2 = new SocketSession(_sessionOptions, _loggerMock.Object);
        
//         session1.CreateSessionId();
//         session2.CreateSessionId();
        
//         // 리플렉션으로 내부 패킷 버퍼 접근
//         var packetBufferField = typeof(SocketSession)
//             .GetField("_packetBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
//         var packetBuffer1 = packetBufferField?.GetValue(session1);
//         var packetBuffer2 = packetBufferField?.GetValue(session2);
        
//         var appendMethod = packetBuffer1?.GetType().GetMethod("Append", new[] { typeof(ReadOnlyMemory<byte>) });
//         var tryReadMethod = packetBuffer1?.GetType().GetMethod("TryReadPacket", new[] { 
//             typeof(ReadOnlyMemory<byte>).MakeByRefType(), 
//             typeof(byte[]).MakeByRefType() 
//         });
        
//         // Act - 첫 번째 세션에 패킷 데이터 추가
//         // 패킷 헤더(길이 2바이트) + 패킷 타입(1바이트) + 페이로드(3바이트)
//         byte[] packetData1 = new byte[] { 0, 4, 1, 1, 2, 3 }; // 길이 4, 타입 1, 데이터 1,2,3
//         appendMethod?.Invoke(packetBuffer1, new object[] { new ReadOnlyMemory<byte>(packetData1) });
        
//         // 두 번째 세션에 다른 패킷 데이터 추가
//         byte[] packetData2 = new byte[] { 0, 5, 2, 5, 6, 7, 8 }; // 길이 5, 타입 2, 데이터 5,6,7,8
//         appendMethod?.Invoke(packetBuffer2, new object[] { new ReadOnlyMemory<byte>(packetData2) });
        
//         // 첫 번째 세션에서 패킷 읽기
//         var readOnlyMemoryRef1 = typeof(ReadOnlyMemory<byte>).MakeByRefType();
//         var byteArrayRef = typeof(byte[]).MakeByRefType();
        
//         object[] args1 = new object[] { 
//             ReadOnlyMemory<byte>.Empty, // out parameter placeholder
//             null  // out parameter placeholder for rentedBuffer
//         };
        
//         var result1 = (bool)tryReadMethod.Invoke(packetBuffer1, args1);
//         var packet1 = (ReadOnlyMemory<byte>)args1[0];
        
//         // 두 번째 세션에서 패킷 읽기
//         object[] args2 = new object[] { 
//             ReadOnlyMemory<byte>.Empty, 
//             null
//         };
        
//         var result2 = (bool)tryReadMethod.Invoke(packetBuffer2, args2);
//         var packet2 = (ReadOnlyMemory<byte>)args2[0];
        
//         // Assert - 두 세션이 각각 독립적으로 데이터를 처리하는지 확인
//         Assert.True(result1);
//         Assert.True(result2);
        
//         // 패킷 내용 확인 (첫 바이트로 타입 확인)
//         Assert.Equal(1, packet1.Span[0]); // 첫 번째 세션은 타입 1
//         Assert.Equal(2, packet2.Span[0]); // 두 번째 세션은 타입 2
        
//         // 패킷 길이 확인
//         Assert.Equal(4, packet1.Length);
//         Assert.Equal(5, packet2.Length);
        
//         _testOutputHelper.WriteLine($"세션1 패킷 - 타입: {packet1.Span[0]}, 길이: {packet1.Length}");
//         _testOutputHelper.WriteLine($"세션2 패킷 - 타입: {packet2.Span[0]}, 길이: {packet2.Length}");
//     }

//     //#region Helper Methods

//     // 가짜 소켓 생성
//     private Socket SetupMockSocket()
//     {
//         var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//         listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
//         listener.Listen(1);

//         var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//         client.Connect(listener.LocalEndPoint!);

//         var server = listener.Accept();
//         listener.Close();

//         // 이 테스트용 소켓을 서버 측에서 처리하기 위한 별도 스레드 생성
//         _ = Task.Run(() =>
//         {
//             try
//             {
//                 var buffer = new byte[1024];
//                 while (server.Connected)
//                 {
//                     try
//                     {
//                         server.Receive(buffer);
//                     }
//                     catch
//                     {
//                         break;
//                     }
//                 }
//             }
//             finally
//             {
//                 server.Close();
//             }
//         });

//         return client;
//     }

//     //// 패킷 수신 시뮬레이션
//     //private async Task SimulatePacketReceived(SocketSession session, Packet packet)
//     //{
//     //    // 세션의 OnReceiveCompleted 메서드를 직접 호출할 수 없으므로 
//     //    // 리플렉션을 사용하거나 테스트용 이벤트를 발생시켜야 함
//     //    // 여기서는 세션이 구독하는 이벤트를 통해 시뮬레이션

//     //    // 세션에 SessionDataReceived 이벤트 핸들러가 등록되어 있는지 확인
//     //    if (session.SessionDataReceived == null)
//     //    {
//     //        throw new InvalidOperationException("SessionDataReceived 핸들러가 등록되지 않았습니다");
//     //    }

//     //    // 이벤트 시뮬레이션
//     //    var eventArgs = new SessionDataEventArgs(session, packet);
//     //    var result = await session.SessionDataReceived.Invoke(session, eventArgs);

//     //    // 큐에 추가 검증
//     //    if (result)
//     //    {
//     //        _queueMock.Verify(q => q.OnRecvEnqueueAsync(It.IsAny<ReceivedPacket>()), Times.AtLeastOnce);
//     //    }
//     //}

//     //#endregion
// }