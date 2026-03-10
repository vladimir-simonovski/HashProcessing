using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HashProcessing.Messaging;

public abstract class RabbitMqConsumer<TMessage> where TMessage : MessageBase
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly string _queueName;
    private readonly ushort _prefetchCount;

    protected RabbitMqConsumer(
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string queueName,
        ushort prefetchCount = 1)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;

        ArgumentOutOfRangeException.ThrowIfZero(prefetchCount);
        _prefetchCount = prefetchCount;
    }

    protected abstract Task HandleMessageAsync(TMessage message, IServiceScope scope, CancellationToken ct);

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

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _prefetchCount, global: false, cancellationToken: ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(ea.Body.Span);

                if (message is null)
                {
                    _logger.LogWarning("Consumer {ConsumerId}: received null {MessageType}, nacking",
                        consumerId, typeof(TMessage).Name);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    return;
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                await HandleMessageAsync(message, scope, ct);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                _logger.LogDebug("Consumer {ConsumerId}: processed {MessageType}",
                    consumerId, typeof(TMessage).Name);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — let the message be requeued
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer {ConsumerId}: failed to process {MessageType}",
                    consumerId, typeof(TMessage).Name);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("Consumer {ConsumerId}: listening on queue '{QueueName}'",
            consumerId, _queueName);

        await tcs.Task;
    }
}
