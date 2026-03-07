using System.Text.Json;
using HashProcessing.Contracts;
using HashProcessing.Worker.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HashProcessing.Worker.Infrastructure;

public class RabbitMqHashConsumer
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqHashConsumer> _logger;
    private readonly string _queueName;

    public RabbitMqHashConsumer(
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqHashConsumer> logger,
        string queueName)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;
    }

    public async Task ConsumeAsync(int consumerId, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(
            _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<HashBatchMessage>(ea.Body.Span);

                if (message is null)
                {
                    _logger.LogWarning("Consumer {ConsumerId}: received null message, nacking", consumerId);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    return;
                }

                var entities = message.ToEntities();
                var command = new ProcessReceivedHashesCommand(entities);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ProcessReceivedHashesCommandHandler>();
                await handler.HandleAsync(command, ct);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                _logger.LogDebug("Consumer {ConsumerId}: processed batch of {Count} hashes", consumerId, entities.Count);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — let the message be requeued
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer {ConsumerId}: failed to process message", consumerId);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("Consumer {ConsumerId}: listening on queue '{QueueName}'", consumerId, _queueName);

        await tcs.Task;
    }
}
