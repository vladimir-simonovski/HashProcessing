using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace HashProcessing.Messaging;

public class RabbitMqPublisher(
    IConnection connection,
    ILogger<RabbitMqPublisher> logger,
    string queueName) : IAsyncDisposable
{
    private readonly IConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly ILogger<RabbitMqPublisher> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _queueName = !string.IsNullOrWhiteSpace(queueName)
        ? queueName
        : throw new ArgumentException("Queue name must not be null or whitespace.", nameof(queueName));
    private IChannel? _channel;

    private readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder()
                .Handle<AlreadyClosedException>()
                .Handle<BrokerUnreachableException>()
                .Handle<IOException>()
                .Handle<SocketException>(),
            OnRetry = args =>
            {
                logger.LogWarning(args.Outcome.Exception,
                    "Publish attempt {AttemptNumber} to queue '{QueueName}' failed, retrying",
                    args.AttemptNumber, queueName);
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : MessageBase
    {
        ArgumentNullException.ThrowIfNull(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _retryPipeline.ExecuteAsync(async token =>
        {
            var channel = await GetOrCreateChannelAsync(token);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: token);
        }, ct);

        _logger.LogDebug("Published {MessageType} to queue '{QueueName}'", typeof(TMessage).Name, _queueName);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        if (_channel is not null)
        {
            try { await _channel.DisposeAsync(); }
            catch { /* channel already dead */ }
        }

        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        GC.SuppressFinalize(this);
    }
}
