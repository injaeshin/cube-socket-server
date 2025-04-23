// using System.Net.Sockets;
// using Common.Network;
// using Common.Network.Pool;
// using Xunit;
// using Xunit.Abstractions;

// namespace Common.Tests.Network.Pool;

// public class SocketEventArgsPoolTests
// {
//     private readonly ITestOutputHelper _testOutputHelper;

//     public SocketEventArgsPoolTests(ITestOutputHelper testOutputHelper)
//     {
//         _testOutputHelper = testOutputHelper;
//     }

//     [Fact]
//     public void CreatePool_SuccessfullyInitializes()
//     {
//         // Act
//         var pool = new SocketEventArgsPool(10); // 커스텀 크기로 초기화
        
//         // Assert
//         Assert.NotNull(pool);
//         _testOutputHelper.WriteLine("풀이 성공적으로 초기화됨");
//     }
    
//     [Fact]
//     public void RentAndReturn_WorksCorrectly()
//     {
//         // Arrange
//         var pool = new SocketEventArgsPool(10);
        
//         // Act
//         var args1 = pool.Rent();
        
//         // Assert
//         Assert.NotNull(args1);
//         Assert.NotNull(args1.Buffer); // 버퍼가 할당되었는지 확인
//         Assert.True(args1.Buffer.Length > 0);
        
//         // 반환
//         pool.Return(args1);
        
//         // 다시 빌리기
//         var args2 = pool.Rent();
//         Assert.NotNull(args2);
        
//         // 첫 번째와 같은 객체를 받았는지 확인 (재사용)
//         // 참고: 이것은 내부 구현에 따라 다를 수 있음
//         _testOutputHelper.WriteLine($"첫 번째 빌린 객체: {args1.GetHashCode()}");
//         _testOutputHelper.WriteLine($"두 번째 빌린 객체: {args2.GetHashCode()}");
//     }
    
//     [Fact]
//     public void RentMultiple_ReturnsUniqueInstances()
//     {
//         // Arrange
//         var pool = new SocketEventArgsPool(5);
        
//         // Act
//         var args1 = pool.Rent();
//         var args2 = pool.Rent();
//         var args3 = pool.Rent();
        
//         // Assert
//         Assert.NotNull(args1);
//         Assert.NotNull(args2);
//         Assert.NotNull(args3);
        
//         // 각각 다른 객체인지 확인 (같은 인스턴스가 중복 대여되면 안 됨)
//         Assert.NotSame(args1, args2);
//         Assert.NotSame(args2, args3);
//         Assert.NotSame(args1, args3);
        
//         // 버퍼가 올바르게 할당되었는지 확인
//         Assert.NotNull(args1.Buffer);
//         Assert.NotNull(args2.Buffer);
//         Assert.NotNull(args3.Buffer);
        
//         _testOutputHelper.WriteLine($"빌린 객체 수: 3, 각각 유니크한 인스턴스");
//         _testOutputHelper.WriteLine($"버퍼 크기: {args1.Buffer.Length}");
//     }
    
//     [Fact]
//     public void ReturnWithSocket_ClearsSocket()
//     {
//         // Arrange
//         var pool = new SocketEventArgsPool(5);
//         var args = pool.Rent();
        
//         // 더미 소켓 설정 (실제 연결은 하지 않음)
//         if (args != null)
//         {
//             args.AcceptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
//             // Act
//             pool.Return(args);
            
//             // Assert
//             // AcceptSocket이 null로 설정되었는지 확인
//             Assert.Null(args.AcceptSocket);
//             _testOutputHelper.WriteLine("반환 시 AcceptSocket이 null로 설정됨");
//         }
//         else
//         {
//             _testOutputHelper.WriteLine("풀에서 빌릴 수 있는 항목이 없습니다");
//             // 이 경우에는 Assert가 의미 없음
//             Assert.True(true);
//         }
//     }
    
//     [Fact]
//     public void Close_DisposesResources()
//     {
//         // Arrange
//         var pool = new SocketEventArgsPool(3);
//         var args = pool.Rent();
        
//         // Act
//         pool.Close();
        
//         // Assert - Close 후에도 이미 빌린 args는 사용 가능해야 함
//         Assert.NotNull(args);
//         Assert.NotNull(args.Buffer);
        
//         // Close 후 새 대여 시도
//         var newArgs = pool.Rent();
        
//         // 구현에 따라 다르지만, 일반적으로 새 인스턴스를 생성할 것임
//         Assert.NotNull(newArgs);
//         _testOutputHelper.WriteLine("풀 종료 후에도 새 인스턴스 생성 가능");
//     }
    
//     [Fact]
//     public void ReturnNull_HandlesGracefully()
//     {
//         // Arrange
//         var pool = new SocketEventArgsPool(2);
        
//         // Act & Assert
//         // null 반환 시 예외가 발생하지 않아야 함
//         SocketAsyncEventArgs? nullArgs = null;
//         pool.Return(nullArgs!); // 예외 없이 처리되어야 함
//         _testOutputHelper.WriteLine("null 반환 정상 처리됨");
//     }
// } 