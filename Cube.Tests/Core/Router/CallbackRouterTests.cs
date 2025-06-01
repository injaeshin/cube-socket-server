using Cube.Core.Router;

namespace Cube.Tests.Core.Router;

public class FunctionRouterTests
{
    [Fact]
    public void AddAction_And_InvokeAction_ShouldWork()
    {
        // Arrange
        var router = new FunctionRouter();
        int result = 0;
        router.AddAction<int>(x => result = x);

        // Act
        router.InvokeAction(42);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void AddFunc_And_InvokeFunc_ShouldWork()
    {
        // Arrange
        var router = new FunctionRouter();
        router.AddFunc<string, int>(s => s.Length);

        // Act
        var length = router.InvokeFunc<string, int>("hello");

        // Assert
        Assert.Equal(5, length);
    }

    [Fact]
    public void AddAction_Duplicate_ShouldThrow()
    {
        // Arrange
        var router = new FunctionRouter();
        router.AddAction<int>(_ => { });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => router.AddAction<int>(_ => { }));
    }

    [Fact]
    public void AddFunc_Duplicate_ShouldThrow()
    {
        // Arrange
        var router = new FunctionRouter();
        router.AddFunc<int, int>(x => x);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => router.AddFunc<int, int>(x => x));
    }

    [Fact]
    public void InvokeAction_WithoutRegistration_ShouldThrow()
    {
        // Arrange
        var router = new FunctionRouter();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => router.InvokeAction(1));
    }

    [Fact]
    public void InvokeFunc_WithoutRegistration_ShouldThrow()
    {
        // Arrange
        var router = new FunctionRouter();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => router.InvokeFunc<int, int>(1));
    }
}
