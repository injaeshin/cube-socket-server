using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Pool;

public class BufferPool
{
    private readonly byte[] _buffer;
    private readonly int _totalBytes;
    private readonly int _bufferSize;
    private int _currentIndex;
    private readonly ConcurrentStack<int> _freeIndexPool;
    private readonly ILogger _logger;
    private bool _closed;

    public BufferPool(ILoggerFactory loggerFactory, int totalBytes, int bufferSize)
    {
        _totalBytes = totalBytes;
        _bufferSize = bufferSize;
        _buffer = new byte[_totalBytes];
        _currentIndex = 0;
        _freeIndexPool = new ConcurrentStack<int>();
        _logger = loggerFactory.CreateLogger<BufferPool>();
    }

    public Memory<byte> AllocateBuffer()
    {
        if (_closed) throw new ObjectDisposedException(nameof(BufferPool));

        if (_freeIndexPool.Count > 0)
        {
            if (!_freeIndexPool.TryPop(out var index))
            {
                _logger.LogError("Failed to pop index from free index pool.");
                return Memory<byte>.Empty;
            }

            Array.Clear(_buffer, index * _bufferSize, _bufferSize);
            _logger.LogTrace("Reused buffer: Index {Index}, Offset: {Offset}", index, index * _bufferSize);
            return _buffer.AsMemory(index * _bufferSize, _bufferSize);
        }

        if (_totalBytes - _bufferSize < _currentIndex)
        {
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
        if (_closed) return;

        if (MemoryMarshal.TryGetArray(bufferToReturn, out ArraySegment<byte> segment))
        {
            if (ReferenceEquals(segment.Array, _buffer) && segment.Count == _bufferSize)
            {
                int index = segment.Offset / _bufferSize;
                _freeIndexPool.Push(index);
                _logger.LogTrace("Buffer returned: Index {Index}, Offset: {Offset}", index, segment.Offset);
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

    public void Close()
    {
        if (_closed) return;
        _closed = true;

        _freeIndexPool.Clear();
    }
}
