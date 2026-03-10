namespace HashProcessing.Api.Infrastructure;

public class HashDailyCountEventBackgroundService(
    HashDailyCountEventConsumer consumer,
    ILogger<HashDailyCountEventBackgroundService> logger) : BackgroundService
{
    private readonly HashDailyCountEventConsumer _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    private readonly ILogger<HashDailyCountEventBackgroundService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting hash daily count event consumer");
        await _consumer.ConsumeAsync(consumerId: 1, stoppingToken);
    }
}
