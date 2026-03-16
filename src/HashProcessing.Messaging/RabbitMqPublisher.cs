using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace HashProcessing.Messaging;

public class RabbitMqPublisher(
    PublisherChannelPool channelPool,
    ILogger<RabbitMqPublisher> logger)
{
    private readonly PublisherChannelPool _channelPool = channelPool ?? throw new ArgumentNullException(nameof(channelPool));
    private readonly ILogger<RabbitMqPublisher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, byte> _declaredQueues = new();

    private static readonly PredicateBuilder<object> TransientPredicate = new PredicateBuilder<object>()
        .Handle<AlreadyClosedException>()
        .Handle<BrokerUnreachableException>()
        .Handle<IOException>()
        .Handle<SocketException>();

    private readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = TransientPredicate,
            OnRetry = args =>
            {
                logger.LogWarning(args.Outcome.Exception,
                    "Publish attempt {AttemptNumber} failed, retrying",
                    args.AttemptNumber);
                return ValueTask.CompletedTask;
            }
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = TransientPredicate,
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
            OnOpened = args =>
            {
                logger.LogError(args.Outcome.Exception,
                    "Circuit breaker opened for {BreakDuration}s",
                    args.BreakDuration.TotalSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                logger.LogInformation("Circuit breaker closed, resuming normal operations");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                logger.LogInformation("Circuit breaker half-opened, testing with next request");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public async Task PublishAsync<TMessage>(
        TMessage message,
        string queueName,
        QueueArguments? queueArguments = null,
        CancellationToken ct = default)
        where TMessage : MessageBase
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            await using var lease = await _channelPool.AcquireAsync(token);
            var channel = lease.Channel;

            if (_declaredQueues.TryAdd(queueName, 0))
            {
                try
                {
                    await channel.QueueDeclareAsync(
                        queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: queueArguments?.ToDictionary(),
                        cancellationToken: token);
                }
                catch
                {
                    _declaredQueues.TryRemove(queueName, out _);
                    throw;
                }
            }

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: token);
        }, ct);

        _logger.LogDebug("Published {MessageType} to queue '{QueueName}'", typeof(TMessage).Name, queueName);
    }
}
