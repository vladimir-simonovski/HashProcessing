using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class BenchmarkWorkerFactory(
    RabbitMqFixture rabbitMq,
    MariaDbFixture mariaDb,
    ushort? prefetchCount = null)
    : IAsyncDisposable
{
    private readonly RabbitMqFixture _rabbitMq = rabbitMq ?? throw new ArgumentNullException(nameof(rabbitMq));
    private readonly MariaDbFixture _mariaDb = mariaDb ?? throw new ArgumentNullException(nameof(mariaDb));
    private IHost? _host;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not started.");

    public async Task StartAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MariaDb"] = _mariaDb.WorkerConnectionString
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<HashProcessing.Worker.Worker>();

        builder.Services.RemoveAll<DbContextOptions<HashDbContext>>();
        builder.Services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(_mariaDb.WorkerConnectionString, new MariaDbServerVersion(new Version(11, 0))));

        builder.Services.RemoveAll<IConnectionFactory>();
        builder.Services.AddSingleton(_rabbitMq.ConnectionFactory);

        if (prefetchCount.HasValue)
        {
            var prefetch = prefetchCount.Value;
            builder.Services.RemoveAll<RabbitMqHashConsumer>();
            builder.Services.AddSingleton(sp =>
                new RabbitMqHashConsumer(
                    sp.GetRequiredService<IConnection>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<ILogger<RabbitMqHashConsumer>>(),
                    "hash-processing",
                    prefetch,
                    new QueueArguments { DeadLetterExchange = "dlx" }));
        }

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
