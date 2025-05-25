using Microsoft.Extensions.Logging;

namespace Cube.Server.Chat.Helper;

public static class LoggerFactoryHelper
{
    private static ILoggerFactory? _loggerFactory;

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public static ILogger<T> CreateLogger<T>()
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("LoggerFactoryHelper is not initialized.");
        return _loggerFactory.CreateLogger<T>();
    }

    public static ILogger CreateLogger(string categoryName)
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("LoggerFactoryHelper is not initialized.");
        return _loggerFactory.CreateLogger(categoryName);
    }

    public static ILoggerFactory GetLoggerFactory()
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("LoggerFactoryHelper is not initialized.");
        return _loggerFactory;
    }
}
