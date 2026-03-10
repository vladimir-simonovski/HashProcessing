using HashProcessing.Api.Application;
using HashProcessing.Messaging;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public class HashDailyCountEventConsumer(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<HashDailyCountEventConsumer> logger,
    string queueName)
    : RabbitMqConsumer<HashDailyCountMessage>(connectionFactory, scopeFactory, logger, queueName)
{
    protected override async Task HandleMessageAsync(
        HashDailyCountMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<UpsertHashDailyCountCommandHandler>();
        await handler.HandleAsync(new UpsertHashDailyCountCommand(message.Date, message.Count), ct);
    }
}
