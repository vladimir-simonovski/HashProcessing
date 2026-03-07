using HashProcessing.Worker.Infrastructure;

namespace HashProcessing.Worker;

public class Worker(RabbitMqHashConsumer consumer, ILogger<Worker> logger) : BackgroundService
{
    private readonly RabbitMqHashConsumer _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    private readonly ILogger<Worker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private const int DegreeOfParallelism = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting {Count} parallel consumers", DegreeOfParallelism);

        var tasks = Enumerable.Range(1, DegreeOfParallelism)
            .Select(id => _consumer.ConsumeAsync(id, stoppingToken));

        await Task.WhenAll(tasks);
    }
}