using Cube.Core.Network;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;


namespace Cube.Tests.Network;

public class ProcessChannelTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _outputHelper;

    public ProcessChannelTests(ITestOutputHelper output)
    {
        _outputHelper = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole()
                .AddProvider(new XunitLoggerProvider(_outputHelper));
        });
    }

    [Fact]
    public async Task ProcessChannel_EnqueueAndProcess_ShouldWorkCorrectly()
    {
        // Arrange
        var processedCount = 0;
        async Task OnProcess(ReceivedContext context)
        {
            Interlocked.Increment(ref processedCount);
            _outputHelper.WriteLine($"Processed message from session: {context.SessionId}");
            await Task.CompletedTask;
        }

        using var channel = new ProcessChannel(_loggerFactory, OnProcess);

        // Act
        var data = new byte[] { 1, 2, 3, 4 };
        await channel.EnqueueAsync(new ReceivedContext("t_session", 1, data, null));

        // Allow time for processing
        await Task.Delay(100);

        // Assert
        Assert.Equal(1, processedCount);
    }

    internal void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}