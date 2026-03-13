using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class RabbitMqFixture : IAsyncDisposable
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .Build();

    public IConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async Task StartAsync()
    {
        await _container.StartAsync();

        ConnectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_container.GetConnectionString()),
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task PurgeQueueAsync(string queueName, IDictionary<string, object?>? queueArguments = null)
    {
        await using var connection = await ConnectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments);
        await channel.QueuePurgeAsync(queueName);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
