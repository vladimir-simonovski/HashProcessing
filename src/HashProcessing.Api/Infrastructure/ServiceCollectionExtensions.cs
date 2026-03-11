using HashProcessing.Api.Core;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace HashProcessing.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: Replace hardcoded values with IConfiguration-based options (e.g., IOptions<T> pattern).
        const ushort channelCapacity = 128;
        const ushort degreeOfParallelism = 0; // 0 = Environment.ProcessorCount
        const ushort batchSize = 100;
        const string publishQueueName = "hash-processing";
        const string consumeQueueName = "hash-daily-counts";
        var rabbitMqHost = configuration["RabbitMQ:HostName"] ?? "localhost";
        var connectionString = configuration.GetConnectionString("MariaDb")
                               ?? "Server=localhost;Database=api;User=root;Password=root;";

        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = rabbitMqHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        });

        services.AddDbContext<ApiDbContext>(options =>
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 0))));

        services.AddScoped<IHashDailyCountRepository, HashDailyCountRepository>();

        services.AddTransient<IHashGenerator>(_ =>
            new DefaultHashGenerator(channelCapacity));

        services.AddSingleton<IHashProcessor>(sp =>
            new RabbitMqBatchedOffloadToWorkerProcessor(
                sp.GetRequiredService<IConnectionFactory>(),
                sp.GetRequiredService<ILoggerFactory>(),
                degreeOfParallelism,
                batchSize,
                publishQueueName));

        services.AddSingleton(sp =>
            new HashDailyCountEventConsumer(
                sp.GetRequiredService<IConnectionFactory>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<HashDailyCountEventConsumer>>(),
                consumeQueueName));

        services.AddHostedService<HashDailyCountEventBackgroundService>();

        return services;
    }
}