using JetBrains.Annotations;
using Testcontainers.MariaDb;

namespace HashProcessing.IntegrationTests.Fixtures;

[UsedImplicitly]
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly RabbitMqFixture _rabbitMq = new();

    private readonly MariaDbContainer _mariaDb = new MariaDbBuilder()
        .WithImage("mariadb:11")
        .Build();

    public ApiWebApplicationFactory ApiFactory { get; private set; } = null!;
    private WorkerApplicationFactory WorkerFactory { get; set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _mariaDb.StartAsync(),
            _rabbitMq.InitializeAsync());

        var dbConnectionString = _mariaDb.GetConnectionString();

        WorkerFactory = new WorkerApplicationFactory(dbConnectionString, _rabbitMq.ConnectionString);
        await WorkerFactory.InitializeAsync();

        ApiFactory = new ApiWebApplicationFactory(dbConnectionString, _rabbitMq.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await WorkerFactory.DisposeAsync();
        await ApiFactory.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _mariaDb.DisposeAsync();
    }
}
