using System.Collections.Concurrent;

namespace Cube.Core.Pool;

public class ObjectPool<T> : IAsyncResource where T : class, IDisposable
{
    private readonly ConcurrentStack<T> _pool;
    private readonly Func<T> _factory;
    private readonly TaskCompletionSource _allReturned = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _inUseCount = 0;
    private volatile bool _stopped = false;

    public ObjectPool(Func<T> factory, int initialSize = 10)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _pool = new ConcurrentStack<T>();

        for (int i = 0; i < initialSize; i++)
        {
            _pool.Push(_factory.Invoke());
        }
    }

    public T Rent()
    {
        if (_stopped) throw new ObjectDisposedException(nameof(ObjectPool<T>));

        Interlocked.Increment(ref _inUseCount);

        if (_pool.TryPop(out var item))
        {
            return item;
        }

        return _factory.Invoke();
    }

    public void Return(T obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        if (_stopped)
        {
            obj.Dispose();
        }
        else
        {
            _pool.Push(obj);
        }

        if (Interlocked.Decrement(ref _inUseCount) == 0 && _stopped)
        {
            _allReturned.TrySetResult();
        }
    }

    public string Name { get; init; } = "ObjectPool";
    public int Count => _pool.Count;
    public int InUse => _inUseCount;

    public async Task StopAsync(TimeSpan timeout)
    {
        await CloseAsync(timeout);
    }

    private async Task CloseAsync(TimeSpan timeout)
    {
        if (_stopped) throw new InvalidOperationException("ObjectPool is already closed");

        _stopped = true;

        // In case nothing is in use, we complete right away
        if (Interlocked.CompareExchange(ref _inUseCount, 0, 0) == 0)
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
            // 강제 종료 로그 등 남길 수 있음
        }

        // 풀에 남아 있는 객체들을 정리
        while (_pool.TryPop(out var item))
        {
            item.Dispose();
        }

        _pool.Clear();
    }
}