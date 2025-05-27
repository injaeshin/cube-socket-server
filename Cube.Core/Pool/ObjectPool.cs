using System.Collections.Concurrent;

namespace Cube.Core.Pool;

public class ObjectPool<T> where T : class, IDisposable
{
    private readonly ConcurrentStack<T> _pool;
    private readonly Func<T> _factory;

    private bool _closed = false;

    public ObjectPool(Func<T> factory, int initialSize = 10)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _pool = new ConcurrentStack<T>();

        for (int i = 0; i < initialSize; i++)
        {
            _pool.Push(_factory.Invoke());
        }
    }

    public int Count => _pool.Count;

    public T Rent()
    {
        if (_closed) throw new ObjectDisposedException(nameof(ObjectPool<T>));

        if (_pool.TryPop(out var item))
        {
            return item;
        }

        return _factory.Invoke();
    }

    public void Return(T obj)
    {
        if (_closed) throw new ObjectDisposedException(nameof(ObjectPool<T>));

        if (obj != null)
        {
            _pool.Push(obj);
        }
        else
        {
            throw new ArgumentNullException(nameof(obj));
        }
    }

    public void Close()
    {
        if (_closed) throw new InvalidOperationException("ObjectPool is closed");
        _closed = true;

        while (_pool.TryPop(out var item))
        {
            item.Dispose();
        }

        _pool.Clear();
    }
}