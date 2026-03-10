using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

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

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : MessageBase
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_channel is null)
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            await _channel.QueueDeclareAsync(
                _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        _logger.LogDebug("Published {MessageType} to queue '{QueueName}'", typeof(TMessage).Name, _queueName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
