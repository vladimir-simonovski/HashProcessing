using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HashProcessing.Messaging;

public abstract class RabbitMqConsumer<TMessage> where TMessage : MessageBase
{
    private readonly IConnection _connection;

    private readonly ILogger _logger;
    private readonly string _queueName;
    private readonly ushort _prefetchCount;
    private readonly QueueArguments? _queueArguments;

    protected RabbitMqConsumer(
        IConnection connection,
        ILogger logger,
        string queueName,
        ushort prefetchCount = 1,
        QueueArguments? queueArguments = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;

        ArgumentOutOfRangeException.ThrowIfZero(prefetchCount);
        _prefetchCount = prefetchCount;

        _queueArguments = queueArguments;
    }

    protected abstract Task HandleMessageAsync(TMessage message, CancellationToken ct);

    public async Task ConsumeAsync(int consumerId, CancellationToken ct)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(
            _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: _queueArguments?.ToDictionary(),
            cancellationToken: ct);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _prefetchCount, global: false, cancellationToken: ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => tcs.TrySetCanceled(ct));

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var ch = ((AsyncEventingBasicConsumer)sender).Channel;
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(ea.Body.Span);

                if (message is null)
                {
                    _logger.LogWarning("Consumer {ConsumerId}: received null {MessageType}, nacking",
                        consumerId, typeof(TMessage).Name);
                    await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
                    return;
                }
                
                await HandleMessageAsync(message, ct);

                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
                _logger.LogDebug("Consumer {ConsumerId}: processed {MessageType}",
                    consumerId, typeof(TMessage).Name);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — let the message be requeued
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer {ConsumerId}: failed to process {MessageType}, dead-lettering",
                    consumerId, typeof(TMessage).Name);
                await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("Consumer {ConsumerId}: listening on queue '{QueueName}'",
            consumerId, _queueName);

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
        finally
        {
            try { await channel.BasicCancelAsync(consumerTag, noWait: false, ct); }
            catch { /* channel may already be closed */ }
        }
    }
}
