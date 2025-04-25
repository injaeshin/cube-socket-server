using Microsoft.Extensions.Logging;

public interface ILoggerFactoryHelper
{
    ILogger<T> CreateLogger<T>();
    ILogger CreateLogger(string categoryName);
}

internal class LoggerFactoryHelper : ILoggerFactoryHelper
{
    private readonly ILoggerFactory _loggerFactory;

    public LoggerFactoryHelper(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    public ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
}
