using System.Collections.Concurrent;

namespace Common.Network.Session;

public class ObjectPool<T> where T : class, IDisposable
{
    private readonly ConcurrentStack<T> _pool;
    private readonly Func<T> _factory;

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
        if (_pool.TryPop(out var item))
        {
            return item;
        }

        return _factory.Invoke();
    }
    
    public void Return(T obj)
    {
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
        foreach (var item in _pool)
        {
            item.Dispose();
        }

        _pool.Clear();
    }

    public void Dispose()
    {
        Close();
    }

    public int Count => _pool.Count;
}
