using Microsoft.Extensions.DependencyInjection;

namespace Cube.Server.Chat;

public interface IObjectFactoryHelper
{
    T Create<T>() where T : notnull;
    T CreateWithParameters<T>(params object[] parameters) where T : notnull;
}

public class ObjectFactoryHelper(IServiceProvider serviceProvider) : IObjectFactoryHelper
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public T Create<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public T CreateWithParameters<T>(params object[] parameters) where T : notnull
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider, parameters);
    }
}
