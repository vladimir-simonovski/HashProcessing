using HashProcessing.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class RabbitMqFixture : IAsyncDisposable
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .Build();

    public IConnectionFactory ConnectionFactory { get; private set; } = null!;
    public IConnection Connection { get; private set; } = null!;

    public async Task StartAsync()
    {
        await _container.StartAsync();

        ConnectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_container.GetConnectionString()),
            AutomaticRecoveryEnabled = true
        };

        Connection = await ConnectionFactory.CreateConnectionAsync();
    }

    public async Task PurgeQueueAsync(string queueName, QueueArguments? queueArguments = null)
    {
        await using var channel = await Connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments?.ToDictionary());
        await channel.QueuePurgeAsync(queueName);
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}
