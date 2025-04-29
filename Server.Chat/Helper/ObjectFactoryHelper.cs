using Microsoft.Extensions.DependencyInjection;

namespace Server.Chat.Helper;

public interface IObjectFactoryHelper
{
    T Create<T>() where T : notnull;
}

public class ObjectFactoryHelper(IServiceProvider serviceProvider) : IObjectFactoryHelper
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public T Create<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
