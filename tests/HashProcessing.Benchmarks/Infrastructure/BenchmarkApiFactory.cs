using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ApiProgram = HashProcessing.Api.Program;

namespace HashProcessing.Benchmarks.Infrastructure;

public class BenchmarkApiFactory : WebApplicationFactory<ApiProgram>
{
    private readonly RabbitMqFixture _rabbitMq;
    private readonly MariaDbFixture _mariaDb;
    private readonly CompletionSignal _completionSignal;
    private readonly ushort _batchSize;

    public BenchmarkApiFactory(
        RabbitMqFixture rabbitMq,
        MariaDbFixture mariaDb,
        CompletionSignal completionSignal,
        ushort batchSize)
    {
        _rabbitMq = rabbitMq ?? throw new ArgumentNullException(nameof(rabbitMq));
        _mariaDb = mariaDb ?? throw new ArgumentNullException(nameof(mariaDb));
        _completionSignal = completionSignal ?? throw new ArgumentNullException(nameof(completionSignal));
        _batchSize = batchSize;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiDbContext>>();
            services.AddDbContext<ApiDbContext>(options =>
                options.UseMySql(_mariaDb.ApiConnectionString, new MariaDbServerVersion(new Version(11, 0))));

            services.RemoveAll<IConnectionFactory>();
            services.AddSingleton(_rabbitMq.ConnectionFactory);

            services.Configure<HashProcessingOptions>(o => o.BatchSize = _batchSize);

            services.AddSingleton(_completionSignal);
            services.AddScoped<HashDailyCountRepository>();
            services.RemoveAll<IHashDailyCountRepository>();
            services.AddScoped<IHashDailyCountRepository, CountingHashDailyCountRepository>();
        });
    }
}
