using HashProcessing.Api.Application;
using HashProcessing.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public class HashDailyCountEventConsumer(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<HashDailyCountEventConsumer> logger,
    IOptions<HashProcessingOptions> options)
    : RabbitMqConsumer<HashDailyCountMessage>(
        connection,
        logger,
        options.Value.ConsumeQueueName,
        queueArguments: new QueueArguments { DeadLetterExchange = options.Value.DeadLetterExchange })
{
    protected override async Task HandleMessageAsync(
        HashDailyCountMessage message,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpsertHashDailyCountCommandHandler>();

        await handler.HandleAsync(new UpsertHashDailyCountCommand(message.Date, message.Count), ct);
    }
}
