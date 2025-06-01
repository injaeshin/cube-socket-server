
namespace Cube.Core.Router;

public interface IFunctionRouter
{
    void AddAction<T>(Action<T> action);
    void AddFunc<T, TResult>(Func<T, TResult> func);
    void InvokeAction<T>(T param);
    TResult InvokeFunc<T, TResult>(T param);
}

public class FunctionRouter : IFunctionRouter
{
    private readonly Dictionary<Type, Delegate> _actionRoutes = [];
    private readonly Dictionary<Type, Delegate> _funcRoutes = [];

    public void AddAction<T>(Action<T> action)
    {
        var type = typeof(T);
        if (_actionRoutes.ContainsKey(type))
        {
            throw new InvalidOperationException($"Action for type {type} already exists");
        }

        _actionRoutes[type] = action;
    }

    public void AddFunc<T, TResult>(Func<T, TResult> func)
    {
        var type = typeof(T);
        if (_funcRoutes.ContainsKey(type))
        {
            throw new InvalidOperationException($"Func for type {type} already exists");
        }

        _funcRoutes[type] = func;
    }

    public void InvokeAction<T>(T param)
    {
        if (!_actionRoutes.TryGetValue(typeof(T), out var action))
        {
            throw new InvalidOperationException($"No action found for type {typeof(T)}");
        }

        ((Action<T>)action)(param);
    }

    public TResult InvokeFunc<T, TResult>(T param)
    {
        if (!_funcRoutes.TryGetValue(typeof(T), out var func))
        {
            throw new InvalidOperationException($"No func found for type {typeof(T)}");
        }

        return ((Func<T, TResult>)func)(param);
    }
}
