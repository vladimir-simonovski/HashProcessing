using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using Microsoft.Extensions.Options;

namespace HashProcessing.Worker.Infrastructure;

public class RabbitMqDailyHashCountNotifier(
    RabbitMqPublisher publisher,
    IOptions<WorkerOptions> workerOptions) : IDailyHashCountNotifier
{
    private readonly RabbitMqPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly WorkerOptions _workerOptions = (workerOptions ?? throw new ArgumentNullException(nameof(workerOptions))).Value;
    private string QueueName => _workerOptions.PublishQueueName;
    private QueueArguments? _queueArguments;
    private QueueArguments QueueArguments => _queueArguments ??= new QueueArguments { DeadLetterExchange = _workerOptions.DeadLetterExchange };

    public Task NotifyDailyHashCountsAsync(IReadOnlyDictionary<DateOnly, long> countsByDate, CancellationToken ct = default) => Task.WhenAll(countsByDate.Select(x =>
        _publisher.PublishAsync(new HashDailyCountMessage(x.Key, x.Value), QueueName, QueueArguments, ct)));
}
