using Cube.Core.Network;
using Microsoft.Extensions.Logging;

namespace Cube.Core.Execution;

public interface IProcessor
{
    Task ExecuteAsync(ReceivedContext context);
}

public abstract class Processor : IProcessor
{
    private readonly ILogger _logger;

    public Processor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Processor>();
    }

    public abstract Task ExecuteAsync(ReceivedContext context);
}