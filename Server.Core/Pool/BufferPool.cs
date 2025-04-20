namespace Server.Core.Pool;

public class BufferPool
{
    private readonly int _totalBytes;
    private readonly int _bufferSize;
    private readonly byte[] _buffer;
    private readonly Stack<int> _freeIndexPool;
    private int _currentIndex;

    public BufferPool(int totalBytes, int bufferSize)
    {
        _totalBytes = totalBytes;
        _bufferSize = bufferSize;
        _buffer = new byte[_totalBytes];
        _freeIndexPool = new Stack<int>();
    }

    public ArraySegment<byte> AllocateBuffer()
    {
        if (_freeIndexPool.Count > 0)
        {
            var index = _freeIndexPool.Pop();
            return new ArraySegment<byte>(_buffer, index, _bufferSize);
        }

        if (_totalBytes - _bufferSize < _currentIndex)
        {
            throw new Exception("Buffer exhausted");
        }

        var segment = new ArraySegment<byte>(_buffer, _currentIndex, _bufferSize);
        _currentIndex += _bufferSize;
        return segment;
    }

    public void FreeBuffer(ArraySegment<byte> segment)
    {
        _freeIndexPool.Push(segment.Offset);
    }

    public void Close()
    {
        _freeIndexPool.Clear();
        _currentIndex = 0;
    }
}
