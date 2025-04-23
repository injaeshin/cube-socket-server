// using Common.Network.Pool;
// using Xunit;
// using Xunit.Abstractions;

// namespace Common.Tests.Network.Pool;

// public class BufferPoolTests
// {
//     private readonly ITestOutputHelper _testOutputHelper;

//     public BufferPoolTests(ITestOutputHelper testOutputHelper)
//     {
//         _testOutputHelper = testOutputHelper;
//     }

//     [Fact]
//     public void AllocateBuffer_ReturnsCorrectSize()
//     {
//         // Arrange
//         int totalBytes = 1024;
//         int bufferSize = 256;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // Act
//         var segment = pool.AllocateBuffer();

//         // Assert
//         Assert.NotNull(segment.Array);
//         Assert.Equal(bufferSize, segment.Count);
//         Assert.Equal(0, segment.Offset); // 첫 번째 버퍼의 오프셋은 0

//         _testOutputHelper.WriteLine($"할당된 버퍼 크기: {segment.Count}");
//     }

//     [Fact]
//     public void AllocateMultiple_ReturnsConsecutiveSegments()
//     {
//         // Arrange
//         int totalBytes = 1024;
//         int bufferSize = 256;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // Act
//         var segment1 = pool.AllocateBuffer();
//         var segment2 = pool.AllocateBuffer();
//         var segment3 = pool.AllocateBuffer();

//         // Assert
//         // 같은 기본 배열을 공유하는지 확인
//         Assert.Same(segment1.Array, segment2.Array);
//         Assert.Same(segment2.Array, segment3.Array);

//         // 오프셋이 순차적으로 증가하는지 확인
//         Assert.Equal(0, segment1.Offset);
//         Assert.Equal(bufferSize, segment2.Offset);
//         Assert.Equal(bufferSize * 2, segment3.Offset);

//         // 모든 세그먼트의 크기가 동일한지 확인
//         Assert.Equal(bufferSize, segment1.Count);
//         Assert.Equal(bufferSize, segment2.Count);
//         Assert.Equal(bufferSize, segment3.Count);

//         _testOutputHelper.WriteLine($"버퍼1 오프셋: {segment1.Offset}, 크기: {segment1.Count}");
//         _testOutputHelper.WriteLine($"버퍼2 오프셋: {segment2.Offset}, 크기: {segment2.Count}");
//         _testOutputHelper.WriteLine($"버퍼3 오프셋: {segment3.Offset}, 크기: {segment3.Count}");
//     }

//     [Fact]
//     public void FreeBuffer_AllowsReuse()
//     {
//         // Arrange
//         int totalBytes = 512;
//         int bufferSize = 128;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // Act
//         var segment1 = pool.AllocateBuffer();
//         var segment2 = pool.AllocateBuffer();

//         // segment1 반환
//         pool.FreeBuffer(segment1);

//         // 반환된 세그먼트를 재사용해야 함
//         var segment3 = pool.AllocateBuffer();

//         // Assert
//         // segment3는 segment1의 오프셋을 재사용해야 함
//         Assert.Equal(segment1.Offset, segment3.Offset);

//         _testOutputHelper.WriteLine($"버퍼1 오프셋: {segment1.Offset}");
//         _testOutputHelper.WriteLine($"버퍼3 오프셋: {segment3.Offset}");
//         _testOutputHelper.WriteLine("버퍼 재사용 성공");
//     }

//     [Fact]
//     public void AllocateMoreThanTotal_ThrowsException()
//     {
//         // Arrange
//         int totalBytes = 512;
//         int bufferSize = 128;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // Act & Assert
//         // 가능한 최대 버퍼 수 할당 (512 / 128 = 4)
//         var segment1 = pool.AllocateBuffer();
//         var segment2 = pool.AllocateBuffer();
//         var segment3 = pool.AllocateBuffer();
//         var segment4 = pool.AllocateBuffer();

//         // 더 이상 할당할 수 없음
//         var exception = Assert.Throws<Exception>(() => pool.AllocateBuffer());
//         Assert.Contains("Buffer exhausted", exception.Message);

//         _testOutputHelper.WriteLine($"예상대로 예외 발생: {exception.Message}");
//     }

//     [Fact]
//     public void Close_ResetsPool()
//     {
//         // Arrange
//         int totalBytes = 512;
//         int bufferSize = 128;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // 모든 버퍼 할당
//         var segment1 = pool.AllocateBuffer();
//         var segment2 = pool.AllocateBuffer();
//         var segment3 = pool.AllocateBuffer();
//         var segment4 = pool.AllocateBuffer();

//         // Act
//         pool.Close();

//         // Assert
//         // Close 후에는 다시 할당할 수 있어야 함
//         var newSegment = pool.AllocateBuffer();
//         Assert.Equal(0, newSegment.Offset); // 초기화 후 첫 번째 오프셋

//         _testOutputHelper.WriteLine("풀 초기화 후 재할당 성공");
//     }

//     [Fact]
//     public void AllocateThenFreeAll_AllowsCompleteReuse()
//     {
//         // Arrange
//         int totalBytes = 512;
//         int bufferSize = 128;
//         var pool = new BufferPool(totalBytes, bufferSize);

//         // 모든 버퍼 할당 후 반환
//         var segments = new List<ArraySegment<byte>>();
//         for (int i = 0; i < totalBytes / bufferSize; i++)
//         {
//             segments.Add(pool.AllocateBuffer());
//         }

//         // 모든 버퍼 반환
//         foreach (var segment in segments)
//         {
//             pool.FreeBuffer(segment);
//         }

//         // Act & Assert
//         // 반환 후에는 다시 모든 버퍼를 할당할 수 있어야 함
//         for (int i = 0; i < totalBytes / bufferSize; i++)
//         {
//             var newSegment = pool.AllocateBuffer();
//             Assert.NotNull(newSegment.Array);
//         }

//         _testOutputHelper.WriteLine("모든 버퍼 반환 후 재할당 성공");
//     }
// }