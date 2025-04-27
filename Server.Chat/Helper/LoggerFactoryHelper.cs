using Microsoft.Extensions.Logging;

namespace Server.Chat.Helper;

public interface ILoggerFactoryHelper
{
    ILogger<T> CreateLogger<T>();
    ILogger CreateLogger(string categoryName);
}

internal class LoggerFactoryHelper : ILoggerFactoryHelper
{
    private static readonly Lazy<LoggerFactoryHelper> _instance = new(() => new LoggerFactoryHelper());
    public static LoggerFactoryHelper Instance => _instance.Value;

    private readonly ILoggerFactory _loggerFactory;

    private LoggerFactoryHelper()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            // 로그 출력 형식 설정 
            builder.AddConsole(options =>
            {
                    options.FormatterName = "simple";
                }).AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                    options.SingleLine = true;
                    options.IncludeScopes = true;
                });
        });
    }

    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    public ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
    public ILoggerFactory GetLoggerFactory() => _loggerFactory;
}
