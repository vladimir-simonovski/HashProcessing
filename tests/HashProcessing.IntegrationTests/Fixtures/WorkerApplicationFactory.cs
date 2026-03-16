using HashProcessing.Worker.Application;
using HashProcessing.Worker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace HashProcessing.IntegrationTests.Fixtures;

public class WorkerApplicationFactory(string dbConnectionString, string rabbitMqConnectionString) : IAsyncLifetime
{
    private readonly string _dbConnectionString = dbConnectionString ?? throw new ArgumentNullException(nameof(dbConnectionString));
    private readonly string _rabbitMqConnectionString = rabbitMqConnectionString ?? throw new ArgumentNullException(nameof(rabbitMqConnectionString));
    private IHost? _host;

    public async Task InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:HostName"] = "placeholder",
            ["RabbitMQ:UserName"] = "placeholder",
            ["RabbitMQ:Password"] = "placeholder",
            ["ConnectionStrings:MariaDb"] = _dbConnectionString,
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<HashProcessing.Worker.Worker>();

        builder.Services.RemoveAll<DbContextOptions<HashDbContext>>();
        builder.Services.AddDbContext<HashDbContext>(options =>
            options.UseMySql(_dbConnectionString, new MariaDbServerVersion(new Version(11, 0))));

        builder.Services.RemoveAll<IConnectionFactory>();
        builder.Services.AddSingleton<IConnectionFactory>(new ConnectionFactory
        {
            Uri = new Uri(_rabbitMqConnectionString)
        });

        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();
            await db.Database.MigrateAsync();
        }

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}