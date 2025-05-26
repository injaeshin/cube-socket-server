using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Cube.Network.Channel;
using Cube.Network.Context;

namespace Cube.Benchmark.Network;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ProcessChannelBenchmark
{
    private ILoggerFactory _loggerFactory = null!;
    private byte[] _testData = null!;
    private ProcessChannel _channel = null!;  // 채널을 필드로 이동
    private TaskCompletionSource _completionSource = null!;
    private int _processedCount;

    [Params(1000, 10000, 100000)]
    public int MessageCount { get; set; }

    [Params(128, 1024)]
    public int MessageSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning).AddConsole());
        _testData = new byte[MessageSize];
        new Random(42).NextBytes(_testData);
        
        // 채널 미리 생성
        _completionSource = new TaskCompletionSource();
        _processedCount = 0;
        _channel = new ProcessChannel(_loggerFactory, OnProcess);
    }

    private async Task OnProcess(ReceivedContext context)
    {
        var count = Interlocked.Increment(ref _processedCount);
        if (count == MessageCount)
        {
            _completionSource.SetResult();
        }
        await Task.CompletedTask;
    }

    [Benchmark]
    public async Task ProcessMessages_SimpleWork()
    {
        // 순수하게 메시지 처리만 벤치마크
        for (int i = 0; i < MessageCount; i++)
        {
            await _channel.EnqueueAsync(new ReceivedContext($"session_{i}", _testData, null));
        }
        await _completionSource.Task;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _channel.Close();
        _channel.Dispose();
    }
}