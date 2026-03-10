using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace HashProcessing.Worker.Infrastructure;

public class RabbitMqHashConsumer(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqHashConsumer> logger,
    string queueName)
    : RabbitMqConsumer<HashBatchMessage>(connectionFactory, scopeFactory, logger, queueName)
{
    protected override async Task HandleMessageAsync(
        HashBatchMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var entities = message.ToEntities();
        var command = new ProcessReceivedHashesCommand(entities);

        var handler = scope.ServiceProvider.GetRequiredService<ProcessReceivedHashesCommandHandler>();
        await handler.HandleAsync(command, ct);
    }
}
