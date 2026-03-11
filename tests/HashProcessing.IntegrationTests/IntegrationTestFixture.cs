using Testcontainers.MariaDb;
using Testcontainers.RabbitMq;

namespace HashProcessing.IntegrationTests;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _mariaDb = new MariaDbBuilder()
        .WithImage("mariadb:11")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .Build();

    public ApiWebApplicationFactory ApiFactory { get; private set; } = null!;
    private WorkerApplicationFactory WorkerFactory { get; set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _mariaDb.StartAsync(),
            _rabbitMq.StartAsync());

        var dbConnectionString = _mariaDb.GetConnectionString();
        var rabbitMqConnectionString = _rabbitMq.GetConnectionString();

        WorkerFactory = new WorkerApplicationFactory(dbConnectionString, rabbitMqConnectionString);
        await WorkerFactory.InitializeAsync();

        ApiFactory = new ApiWebApplicationFactory(dbConnectionString, rabbitMqConnectionString);
    }

    public async Task DisposeAsync()
    {
        await WorkerFactory.DisposeAsync();
        await ApiFactory.DisposeAsync();
        await _mariaDb.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
