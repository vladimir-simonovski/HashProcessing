using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class BenchmarkWorkerFactory : IAsyncDisposable
{
    private readonly RabbitMqFixture _rabbitMq;
    private readonly MariaDbFixture _mariaDb;
    private IHost? _host;

    public BenchmarkWorkerFactory(RabbitMqFixture rabbitMq, MariaDbFixture mariaDb)
    {
        _rabbitMq = rabbitMq ?? throw new ArgumentNullException(nameof(rabbitMq));
        _mariaDb = mariaDb ?? throw new ArgumentNullException(nameof(mariaDb));
    }

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not started.");

    public async Task StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<HashProcessing.Worker.Worker>();

        builder.Services.RemoveAll<DbContextOptions<HashDbContext>>();
        builder.Services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(_mariaDb.WorkerConnectionString, new MariaDbServerVersion(new Version(11, 0))));

        builder.Services.RemoveAll<IConnectionFactory>();
        builder.Services.AddSingleton(_rabbitMq.ConnectionFactory);

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
            await db.Database.MigrateAsync();
        }

        await _host.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
