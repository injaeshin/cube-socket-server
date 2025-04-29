using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using System;

namespace Common.Network.Session;

public class SocketEventArgsPool : IDisposable
{
    private readonly BufferPool _eventArgsBuffer;
    private readonly ObjectPool<SocketAsyncEventArgs> _eventArgsPool;
    private readonly ILogger? _logger;
    private bool _disposed = false;

    public int Count => _eventArgsPool.Count;

    // 기본 생성자 - 이전 버전과의 호환성을 위해 유지
    public SocketEventArgsPool() : this(NetConsts.MAX_CONNECTION)
    {
    }

    // maxConnections 파라미터를 받는 새 생성자
    public SocketEventArgsPool(int maxConnections, ILogger? logger = null)
    {
        _logger = logger;
        _eventArgsBuffer = new BufferPool(maxConnections * NetConsts.BUFFER_SIZE, NetConsts.BUFFER_SIZE);
        _eventArgsPool = new ObjectPool<SocketAsyncEventArgs>(() =>
        {
            var args = new SocketAsyncEventArgs();
            return args;
        }, maxConnections);
    }

    public SocketAsyncEventArgs? Rent()
    {
        try
        {
            // 풀에 이벤트 인자가 있는지 확인
            if (_eventArgsPool.Count <= 0)
            {
                _logger?.LogWarning("SocketAsyncEventArgs 풀이 비어 있습니다. 최대 연결 수를 초과했을 수 있습니다.");
                return null;
            }

            // 버퍼 할당 시도
            var buffer = _eventArgsBuffer.AllocateBuffer();
            if (buffer.IsEmpty)
            {
                _logger?.LogWarning("버퍼 할당에 실패했습니다. 사용 가능한 버퍼가 없습니다.");
                return null;
            }

            var args = _eventArgsPool.Rent();
            args.SetBuffer(buffer);

            _logger?.LogDebug("SocketAsyncEventArgs 대여 성공: {ArgsHashCode}", args.GetHashCode());
            return args;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SocketAsyncEventArgs 대여 중 오류 발생");
            return null;
        }
    }

    public void Return(SocketAsyncEventArgs args)
    {
        if (args == null) return;

        try
        {
            // 버퍼가 설정되어 있는 경우에만 해제
            if (args.Buffer != null)
            {
                _eventArgsBuffer.FreeBuffer(args.Buffer);
                args.SetBuffer(Memory<byte>.Empty); // 버퍼 참조 제거
            }

            // Accept 처리된 소켓이 남아 있을 경우 정리
            if (args.AcceptSocket != null)
            {
                args.AcceptSocket = null;
            }

            _eventArgsPool.Return(args);
            _logger?.LogDebug("SocketAsyncEventArgs 반환 성공: {ArgsHashCode}", args.GetHashCode());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SocketAsyncEventArgs 반환 중 오류 발생");
        }
    }

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _eventArgsBuffer?.Dispose();
            _eventArgsPool?.Dispose();
            _logger?.LogInformation("SocketEventArgsPool 정리 완료");
        }
        _disposed = true;
    }

    ~SocketEventArgsPool()
    {
        Dispose(false);
    }
}
