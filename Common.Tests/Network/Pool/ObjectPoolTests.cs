//using Common.Network.Pool;
//using Xunit;
//using Xunit.Abstractions;

//namespace Common.Tests.Network.Pool;

//public class ObjectPoolTests
//{
//    private readonly ITestOutputHelper _testOutputHelper;

//    public ObjectPoolTests(ITestOutputHelper testOutputHelper)
//    {
//        _testOutputHelper = testOutputHelper;
//    }

//    [Fact]
//    public void CreatePool_InitialSizeCorrect()
//    {
//        // Arrange
//        int initialSize = 5;
//        int createdCount = 0;
        
//        // Act
//        var pool = new ObjectPool<string>(() => 
//        {
//            createdCount++;
//            return $"Item {createdCount}";
//        }, initialSize);
        
//        // Assert
//        Assert.Equal(initialSize, pool.Count);
//        _testOutputHelper.WriteLine($"초기 풀 크기: {pool.Count}");
//    }
    
//    [Fact]
//    public void RentAndReturn_WorksCorrectly()
//    {
//        // Arrange
//        int initialSize = 3;
//        int createdCount = 0;
        
//        var pool = new ObjectPool<string>(() => 
//        {
//            createdCount++;
//            return $"Item {createdCount}";
//        }, initialSize);
        
//        // Act & Assert
//        // 초기 풀에서 항목 빌리기
//        string item1 = pool.Rent();
//        // 풀에 있는 항목은 LIFO(Last In First Out) 순서로 반환됨
//        // 따라서 마지막에 생성된 항목부터 나오므로 Item 3이 먼저 나옴
//        Assert.Equal("Item 3", item1);
//        Assert.Equal(initialSize - 1, pool.Count);
//        _testOutputHelper.WriteLine($"첫 번째 항목: {item1}, 남은 풀 크기: {pool.Count}");
        
//        string item2 = pool.Rent();
//        Assert.Equal("Item 2", item2);
//        Assert.Equal(initialSize - 2, pool.Count);
//        _testOutputHelper.WriteLine($"두 번째 항목: {item2}, 남은 풀 크기: {pool.Count}");
        
//        string item3 = pool.Rent();
//        Assert.Equal("Item 1", item3);
//        Assert.Equal(initialSize - 3, pool.Count);
//        _testOutputHelper.WriteLine($"세 번째 항목: {item3}, 남은 풀 크기: {pool.Count}");
        
//        // 풀이 비면 새 항목 생성
//        string item4 = pool.Rent();
//        Assert.Equal("Item 4", item4); // 새로 생성된 항목
//        Assert.Equal(0, pool.Count);
//        _testOutputHelper.WriteLine($"네 번째 항목(새로 생성): {item4}, 남은 풀 크기: {pool.Count}");
        
//        // 항목 반환
//        pool.Return(item1);
//        Assert.Equal(1, pool.Count);
//        _testOutputHelper.WriteLine($"항목 반환 후 풀 크기: {pool.Count}");
        
//        // 반환된 항목 다시 빌리기
//        string rentedAgain = pool.Rent();
//        Assert.Equal(item1, rentedAgain); // 반환된 것이 다시 대여됨
//        Assert.Equal(0, pool.Count);
//        _testOutputHelper.WriteLine($"반환된 항목 다시 빌리기: {rentedAgain}, 남은 풀 크기: {pool.Count}");
//    }
    
//    [Fact]
//    public void Close_ClearsPool()
//    {
//        // Arrange
//        var pool = new ObjectPool<string>(() => "test", 5);
//        Assert.Equal(5, pool.Count);
        
//        // Act
//        pool.Close();
        
//        // Assert
//        Assert.Equal(0, pool.Count);
//        _testOutputHelper.WriteLine("풀 비우기 성공");
//    }
    
//    [Fact]
//    public void MultipleSizeScale_PoolWorks()
//    {
//        // Arrange
//        int initialSize = 2;
//        int rentCount = 10;
//        int createdCount = 0;
        
//        var pool = new ObjectPool<string>(() => 
//        {
//            createdCount++;
//            return $"Item {createdCount}";
//        }, initialSize);
        
//        // Act
//        var rentedItems = new List<string>();
//        for (int i = 0; i < rentCount; i++)
//        {
//            rentedItems.Add(pool.Rent());
//        }
        
//        // Assert - 모든 항목을 빌리면 initialSize + (rentCount - initialSize) 항목이 생성됨
//        Assert.Equal(rentCount, createdCount);
//        Assert.Equal(0, pool.Count);
        
//        // 항목 반환
//        foreach (var item in rentedItems)
//        {
//            pool.Return(item);
//        }
        
//        // 모든 항목이 반환됨
//        Assert.Equal(rentCount, pool.Count);
//        _testOutputHelper.WriteLine($"생성된 항목 수: {createdCount}, 풀 크기: {pool.Count}");
//    }
//} 