using HashProcessing.Messaging;
using HashProcessing.Worker.Core;

namespace HashProcessing.Worker.Application;

public class ProcessReceivedHashesCommand
{
    public IReadOnlyCollection<HashEntity> Entities { get; }

    public ProcessReceivedHashesCommand(IReadOnlyCollection<HashEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (entities.Count == 0)
            throw new ArgumentException("Entities collection must not be empty.", nameof(entities));

        Entities = entities;
    }
}

public class ProcessReceivedHashesCommandHandler(
    IHashRepository repository,
    RabbitMqPublisher publisher,
    ILogger<ProcessReceivedHashesCommandHandler> logger)
{
    private readonly IHashRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly RabbitMqPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly ILogger<ProcessReceivedHashesCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task HandleAsync(ProcessReceivedHashesCommand command, CancellationToken ct = default)
    {
        await _repository.SaveBatchAsync(command.Entities, ct);

        var date = command.Entities.First().Date;
        var totalCount = await _repository.GetCountByDateAsync(date, ct);
        await _publisher.PublishAsync(new HashDailyCountMessage(date, totalCount), ct);

        _logger.LogDebug("Persisted batch of {Count} hashes, published daily count {TotalCount} for {Date}",
            command.Entities.Count, totalCount, date);
    }
}
