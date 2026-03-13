using JetBrains.Annotations;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace HashProcessing.IntegrationTests.Fixtures;

[UsedImplicitly]
public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .Build();

    public IConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _rabbitMq.StartAsync();

        var factory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()) };
        Connection = await factory.CreateConnectionAsync();

        await using var channel = await Connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "dlx",
            type: ExchangeType.Direct,
            durable: true);

        await channel.QueueDeclareAsync("test-dlq", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync("test-dlq", "dlx", routingKey: "test-queue");
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
