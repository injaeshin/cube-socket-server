using System.Collections.Concurrent;

namespace Server.Core.Pool;

public class ObjectPool<T> where T : class
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
        _pool.Clear();
    }

    public int Count => _pool.Count;
}
