using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Cube.Tests;

public class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly string _categoryName;

    public XUnitLogger(ITestOutputHelper outputHelper, string categoryName)
    {
        _outputHelper = outputHelper;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _outputHelper.WriteLine($"{_categoryName} [{logLevel}] {message}");

        if (exception != null)
        {
            _outputHelper.WriteLine(exception.ToString());
        }
    }
}

public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _outputHelper;

    public XunitLoggerProvider(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper; 
    }

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(_outputHelper, categoryName);

    public void Dispose() { }
}
