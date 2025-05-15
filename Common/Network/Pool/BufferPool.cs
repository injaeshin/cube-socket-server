using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Common.Network.Pool;

public class BufferPool : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _totalBytes;
    private readonly int _bufferSize;
    private int _currentIndex;
    private readonly Stack<int> _freeIndexPool;
    private readonly object _lock = new();
    private readonly ILogger _logger;
    private bool _disposed;

    public BufferPool(int totalBytes, int bufferSize, ILoggerFactory loggerFactory)
    {
        _totalBytes = totalBytes;
        _bufferSize = bufferSize;
        _buffer = new byte[_totalBytes];
        _currentIndex = 0;
        _freeIndexPool = new Stack<int>();
        _logger = loggerFactory.CreateLogger<BufferPool>();
    }

    public Memory<byte> AllocateBuffer()
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BufferPool));

            if (_freeIndexPool.Count > 0)
            {
                var index = _freeIndexPool.Pop();
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
    }

    public void FreeBuffer(Memory<byte> bufferToReturn)
    {
        lock (_lock)
        {
            if (_disposed) return;

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
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (disposing)
            {
                _freeIndexPool.Clear();
            }
            _disposed = true;
            _logger.LogInformation("BufferPool Disposed.");
        }
    }
}
