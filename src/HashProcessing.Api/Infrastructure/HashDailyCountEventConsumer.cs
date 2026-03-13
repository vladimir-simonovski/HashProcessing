using HashProcessing.Api.Application;
using HashProcessing.Messaging;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public class HashDailyCountEventConsumer(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<HashDailyCountEventConsumer> logger,
    string queueName,
    IDictionary<string, object?>? queueArguments = null)
    : RabbitMqConsumer<HashDailyCountMessage>(connection, logger, queueName, queueArguments: queueArguments)
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
