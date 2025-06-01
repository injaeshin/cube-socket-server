using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Pool;

public class BufferPool : IAsyncResource
{
    private readonly byte[] _buffer;
    private readonly int _totalBytes;
    private readonly int _bufferSize;
    private int _currentIndex;
    private readonly ConcurrentStack<int> _freeIndexPool;
    private readonly ILogger _logger;

    private int _inUseCount = 0;
    private readonly TaskCompletionSource _allReturned = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _stopped;

    public BufferPool(ILoggerFactory loggerFactory, int totalBytes, int bufferSize)
    {
        _logger = loggerFactory.CreateLogger<BufferPool>();

        _totalBytes = totalBytes;
        _bufferSize = bufferSize;
        _buffer = new byte[_totalBytes];
        _currentIndex = 0;
        _freeIndexPool = new ConcurrentStack<int>();
    }

    public Memory<byte> AllocateBuffer()
    {
        if (_stopped) throw new ObjectDisposedException(nameof(BufferPool));

        Interlocked.Increment(ref _inUseCount);

        //_logger.LogTrace("AllocateBuffer: InUseCount: {InUseCount}, FreeIndexPool: {FreeIndexPool}, StackTrace: {StackTrace}",
        //    _inUseCount,
        //    _freeIndexPool.Count,
        //    Environment.StackTrace);

        if (_freeIndexPool.Count > 0 && _freeIndexPool.TryPop(out var index))
        {
            Array.Clear(_buffer, index * _bufferSize, _bufferSize);
            _logger.LogTrace("Reused buffer: Index {Index}, Offset: {Offset}", index, index * _bufferSize);
            return _buffer.AsMemory(index * _bufferSize, _bufferSize);
        }

        if (_totalBytes - _bufferSize < _currentIndex)
        {
            Interlocked.Decrement(ref _inUseCount);
            _logger.LogError("Buffer pool exhausted. CurrentIndex: {CurrentIndex}, TotalBytes: {TotalBytes}", _currentIndex, _totalBytes);
            return Memory<byte>.Empty;
        }

        var segmentMemory = _buffer.AsMemory(_currentIndex, _bufferSize);
        int allocatedIndex = _currentIndex / _bufferSize;
        _logger.LogTrace("New buffer: Index {Index}, Offset: {Offset}", allocatedIndex, _currentIndex);
        _currentIndex += _bufferSize;

        return segmentMemory;
    }

    public void FreeBuffer(Memory<byte> bufferToReturn)
    {
        if (_stopped) return;

        //_logger.LogTrace("FreeBuffer: InUseCount: {InUseCount}, FreeIndexPool: {FreeIndexPool}, StackTrace: {StackTrace}",
        //    _inUseCount,
        //    _freeIndexPool.Count,
        //    Environment.StackTrace);

        if (MemoryMarshal.TryGetArray(bufferToReturn, out ArraySegment<byte> segment))
        {
            if (ReferenceEquals(segment.Array, _buffer) && segment.Count == _bufferSize)
            {
                int index = segment.Offset / _bufferSize;
                _freeIndexPool.Push(index);
                _logger.LogTrace("Buffer returned: Index {Index}, Offset: {Offset}", index, segment.Offset);

                if (Interlocked.Decrement(ref _inUseCount) == 0 && _stopped)
                {
                    _allReturned.TrySetResult();
                }
            }
            else
            {
                _logger.LogWarning("Tried to return buffer not from this pool or with wrong size.");
            }
        }
        else
        {
            _logger.LogWarning("Tried to return non-array Memory<byte>.");
        }
    }

    public string Name { get; init; } = "BufferPool";
    public int Count => _freeIndexPool.Count;
    public int InUse => _inUseCount;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) return;
        _stopped = true;

        if (_inUseCount == 0)
        {
            _allReturned.TrySetResult();
        }

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _allReturned.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("BufferPool close timeout exceeded; forcing closure with {InUse} buffers still in use.", _inUseCount);
        }

        _freeIndexPool.Clear();
    }
}