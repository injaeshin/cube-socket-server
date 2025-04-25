
using System.Runtime.InteropServices; // MemoryMarshal 사용
using Microsoft.Extensions.Logging; // Logger 사용 시

namespace Common.Network.Session;

public class BufferPool : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _totalBytes;
    private readonly int _bufferSize;
    private int _currentIndex;
    private readonly Stack<int> _freeIndexPool;
    private readonly object _lock = new object(); // 스레드 안전성 위한 lock 객체
    private readonly ILogger? _logger; // 로거 추가 (선택 사항)
    private bool _disposed = false; // Dispose 패턴

    public BufferPool(int totalBytes, int bufferSize, ILogger? logger = null)
    {
        _totalBytes = totalBytes;
        _bufferSize = bufferSize;
        _buffer = new byte[_totalBytes];
        _currentIndex = 0;
        _freeIndexPool = new Stack<int>();
        _logger = logger;
    }

    public Memory<byte> AllocateBuffer()
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BufferPool));

            if (_freeIndexPool.Count > 0)
            {
                var index = _freeIndexPool.Pop();
                Array.Clear(_buffer, index * _bufferSize, _bufferSize); // 인덱스 기반 오프셋 계산
                _logger?.LogTrace("재사용 버퍼 할당: Index {Index}", index);
                return _buffer.AsMemory(index * _bufferSize, _bufferSize);
            }

            if (_totalBytes - _bufferSize < _currentIndex)
            {
                _logger?.LogError("버퍼 풀 고갈됨. 추가 버퍼 할당 불가. CurrentIndex: {_currentIndex}, TotalBytes: {_totalBytes}", _currentIndex, _totalBytes);
                return Memory<byte>.Empty; // 빈 Memory 반환
            }

            var segmentMemory = _buffer.AsMemory(_currentIndex, _bufferSize);
            int allocatedIndex = _currentIndex / _bufferSize; // 로그용 인덱스 계산
            _logger?.LogTrace("새 버퍼 할당: Index {Index}, Offset: {_currentIndex}", allocatedIndex, _currentIndex);
            _currentIndex += _bufferSize;
            return segmentMemory;
        }
    }

    public void FreeBuffer(Memory<byte> bufferToReturn)
    {
        lock (_lock)
        {
            if (_disposed) return; // 이미 Dispose 되었으면 아무것도 안 함

            // MemoryMarshal을 사용해 ArraySegment 얻기
            if (MemoryMarshal.TryGetArray(bufferToReturn, out ArraySegment<byte> segment))
            {
                // 반환된 버퍼가 현재 풀의 버퍼인지 확인 (선택적이지만 안전함)
                if (ReferenceEquals(segment.Array, _buffer) && segment.Count == _bufferSize)
                {
                    // Offset을 이용해 원래 인덱스 계산
                    int index = segment.Offset / _bufferSize;

                    // 버퍼 내용을 초기화할 필요는 보통 없으나, 필요하다면 여기서 Array.Clear(segment.Array, segment.Offset, segment.Count);
                    _freeIndexPool.Push(index);
                    _logger?.LogTrace("버퍼 반환 성공: Index {Index}, Offset: {Offset}", index, segment.Offset);
                }
                else
                {
                    // 다른 풀의 버퍼거나 크기가 안 맞는 경우 로깅
                    _logger?.LogWarning("잘못된 버퍼 반환 시도: 다른 풀의 버퍼거나 크기가 다릅니다.");
                }
            }
            else
            {
                // Array 기반 Memory<byte>가 아닌 경우 (이 시나리오에서는 발생하기 어려움)
                _logger?.LogWarning("Array 기반이 아닌 Memory<byte> 반환 시도.");
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
                // 관리되는 리소스 정리 (예: Stack 클리어)
                _freeIndexPool.Clear();
                // _buffer는 GC가 처리하므로 null 처리 등은 불필요할 수 있음
            }
            // 비관리 리소스 정리는 여기에 (현재는 없음)
            _disposed = true;
            _logger?.LogInformation("BufferPool Disposed.");
        }
    }

    // Close 메서드가 필요하다면 Dispose 호출하도록 구현
    public void Close()
    {
        Dispose();
    }
}
